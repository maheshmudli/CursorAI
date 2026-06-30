using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShiftPlatform.Data;
using ShiftPlatform.Models;
using ShiftPlatform.ViewModels;

namespace ShiftPlatform.Services;

public record MemberCredential(string FullName, string Email, string TempPassword);

public record ProvisioningResult(Tenant Tenant, ApplicationUser Admin, IReadOnlyList<MemberCredential> Members);

public interface IProvisioningService
{
    Task<ProvisioningResult> ProvisionAsync(PendingRegistration pending, RegistrationInput input);
}

/// <summary>
/// Turns a paid <see cref="PendingRegistration"/> into a live tenant: creates the
/// tenant record, the admin and member accounts, seeds template shift data, and
/// writes the provisioning + payment audit records. Runs in a single transaction
/// so a failure leaves nothing behind.
/// </summary>
public class ProvisioningService : IProvisioningService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<ProvisioningService> _logger;

    public ProvisioningService(ApplicationDbContext db, UserManager<ApplicationUser> userManager, ILogger<ProvisioningService> logger)
    {
        _db = db;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<ProvisioningResult> ProvisionAsync(PendingRegistration pending, RegistrationInput input)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();

        var tenant = new Tenant
        {
            CompanyName = input.CompanyName,
            Slug = pending.Slug,
            Status = TenantStatus.Active,
            Tier = TenantTier.Base,
            SeatAllowance = PlatformConstants.IncludedSeats,
            CreatedAt = DateTime.UtcNow
        };
        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync();

        var admin = new ApplicationUser
        {
            UserName = input.AdminEmail,
            Email = input.AdminEmail,
            EmailConfirmed = true,
            FullName = input.AdminFullName,
            TenantId = tenant.Id
        };
        var adminResult = await _userManager.CreateAsync(admin, input.AdminPassword);
        if (!adminResult.Succeeded)
            throw new InvalidOperationException("Failed to create admin: " + string.Join("; ", adminResult.Errors.Select(e => e.Description)));
        await _userManager.AddToRoleAsync(admin, PlatformConstants.Roles.CompanyAdmin);

        var credentials = new List<MemberCredential>();
        foreach (var member in input.Members)
        {
            var tempPassword = GenerateTempPassword();
            var user = new ApplicationUser
            {
                UserName = member.Email,
                Email = member.Email,
                EmailConfirmed = true,
                FullName = member.FullName,
                TenantId = tenant.Id
            };
            var result = await _userManager.CreateAsync(user, tempPassword);
            if (!result.Succeeded)
                throw new InvalidOperationException($"Failed to create user {member.Email}: " + string.Join("; ", result.Errors.Select(e => e.Description)));
            await _userManager.AddToRoleAsync(user, PlatformConstants.Roles.User);
            credentials.Add(new MemberCredential(member.FullName, member.Email, tempPassword));
        }

        SeedTemplateShifts(tenant, admin.Id);

        var route = $"/t/{tenant.Slug}";
        _db.ProvisioningRecords.Add(new ProvisioningRecord
        {
            TenantId = tenant.Id,
            ProvisionedAt = DateTime.UtcNow,
            Route = route,
            Status = "Provisioned"
        });

        _db.PaymentRecords.Add(new PaymentRecord
        {
            TenantId = tenant.Id,
            StripePaymentIntentId = pending.StripePaymentIntentId,
            Amount = pending.Amount,
            Currency = pending.Currency,
            Status = "succeeded",
            CreatedAt = DateTime.UtcNow
        });

        pending.Status = "Provisioned";
        pending.TenantId = tenant.Id;

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        _logger.LogInformation("Provisioned tenant {TenantId} ({Slug}) with {MemberCount} members.", tenant.Id, tenant.Slug, credentials.Count);
        return new ProvisioningResult(tenant, admin, credentials);
    }

    /// <summary>Seeds a couple of starter shifts so a new workspace is not empty.</summary>
    private void SeedTemplateShifts(Tenant tenant, string adminId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var samples = new[]
        {
            new Shift { TenantId = tenant.Id, Date = today.AddDays(1), StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(17, 0), RoleLabel = "Front of House", Notes = "Template starter shift", CreatedBy = adminId },
            new Shift { TenantId = tenant.Id, Date = today.AddDays(2), StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(20, 0), RoleLabel = "Kitchen", Notes = "Template starter shift", CreatedBy = adminId },
            new Shift { TenantId = tenant.Id, Date = today.AddDays(5), StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(16, 0), RoleLabel = "Supervisor", CreatedBy = adminId },
        };
        _db.Shifts.AddRange(samples);
    }

    private static string GenerateTempPassword()
    {
        // Meets the configured Identity policy: upper, lower, digit, symbol.
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnpqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@#$%";
        var rnd = Random.Shared;
        string pwd = $"{upper[rnd.Next(upper.Length)]}{lower[rnd.Next(lower.Length)]}{digits[rnd.Next(digits.Length)]}{symbols[rnd.Next(symbols.Length)]}";
        const string all = upper + lower + digits;
        for (int i = 0; i < 6; i++) pwd += all[rnd.Next(all.Length)];
        return pwd;
    }
}
