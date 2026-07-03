using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShiftPlatform.Constants;
using ShiftPlatform.Data;
using ShiftPlatform.Models;
using ShiftPlatform.Models.Enums;
using ShiftPlatform.ViewModels;

namespace ShiftPlatform.Services;

public interface IProvisioningService
{
    Task<(bool Success, string? Error, Tenant? Tenant, ApplicationUser? Admin)> ProvisionTenantAsync(
        RegistrationSubmission submission,
        RegisterCompanyViewModel model,
        string paymentIntentId);
}

public class ProvisioningService(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager)
    : IProvisioningService
{
    public async Task<(bool Success, string? Error, Tenant? Tenant, ApplicationUser? Admin)> ProvisionTenantAsync(
        RegistrationSubmission submission,
        RegisterCompanyViewModel model,
        string paymentIntentId)
    {
        if (submission.TenantId.HasValue)
        {
            var existingTenant = await dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == submission.TenantId.Value);
            var existingAdmin = await userManager.FindByEmailAsync(model.AdminEmail);
            return (true, null, existingTenant, existingAdmin);
        }

        var slug = CreateSlug(model.CompanyName);
        if (await dbContext.Tenants.AnyAsync(t => t.Slug == slug))
        {
            slug = $"{slug}-{Guid.NewGuid().ToString("N")[..6]}";
        }

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            CompanyName = model.CompanyName.Trim(),
            Slug = slug,
            CreatedAt = DateTime.UtcNow,
            Tier = TenantTier.Base,
            SeatAllowance = 5,
            Status = TenantStatus.Approved
        };

        await using var tx = await dbContext.Database.BeginTransactionAsync();
        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync();

        var admin = new ApplicationUser
        {
            UserName = model.AdminEmail,
            Email = model.AdminEmail,
            FullName = model.AdminFullName,
            TenantId = tenant.Id,
            EmailConfirmed = true
        };

        var adminResult = await userManager.CreateAsync(admin, model.AdminPassword);
        if (!adminResult.Succeeded)
        {
            await tx.RollbackAsync();
            return (false, string.Join("; ", adminResult.Errors.Select(e => e.Description)), null, null);
        }

        await userManager.AddToRoleAsync(admin, ApplicationRoles.CompanyAdmin);

        foreach (var user in model.Users.Where(u => !string.IsNullOrWhiteSpace(u.Email)))
        {
            var tempPassword = $"Tmp!{Guid.NewGuid():N}"[..12] + "aA1!";
            var member = new ApplicationUser
            {
                UserName = user.Email,
                Email = user.Email,
                FullName = user.FullName,
                TenantId = tenant.Id,
                EmailConfirmed = true
            };
            var userResult = await userManager.CreateAsync(member, tempPassword);
            if (!userResult.Succeeded)
            {
                await tx.RollbackAsync();
                return (false, string.Join("; ", userResult.Errors.Select(e => e.Description)), null, null);
            }

            await userManager.AddToRoleAsync(member, ApplicationRoles.User);
        }

        var now = DateTime.UtcNow;
        dbContext.ProvisioningRecords.Add(new ProvisioningRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Route = $"/t/{tenant.Slug}",
            ProvisionedAt = now,
            Status = "Provisioned"
        });

        // Seed workspace template data.
        var seededShift = new Shift
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Date = DateOnly.FromDateTime(now.Date.AddDays(1)),
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(17, 0),
            RoleLabel = "General Shift",
            Notes = "Provisioned template shift",
            CreatedBy = admin.Id,
            CreatedAt = now
        };
        dbContext.Shifts.Add(seededShift);

        submission.TenantId = tenant.Id;
        submission.Status = RegistrationStatus.Provisioned;

        var paymentRecord = await dbContext.PaymentRecords
            .FirstOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntentId);
        if (paymentRecord is not null)
        {
            paymentRecord.TenantId = tenant.Id;
            paymentRecord.Status = "succeeded";
        }

        await dbContext.SaveChangesAsync();
        await tx.CommitAsync();

        return (true, null, tenant, admin);
    }

    private static string CreateSlug(string value)
    {
        var validChars = value
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();
        var slug = new string(validChars);
        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        return slug.Trim('-');
    }
}
