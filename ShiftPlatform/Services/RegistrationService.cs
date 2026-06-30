using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShiftPlatform.Data;
using ShiftPlatform.Infrastructure;
using ShiftPlatform.Models.Entities;
using ShiftPlatform.Models.Enums;
using ShiftPlatform.Models.ViewModels;
using Stripe;

namespace ShiftPlatform.Services;

public interface IRegistrationService
{
    Task<(RegistrationSubmission Submission, PaymentIntent Intent)> StartRegistrationAsync(RegistrationViewModel model, CancellationToken cancellationToken = default);
    Task<RegistrationResult> CompleteRegistrationAsync(CompleteRegistrationViewModel model, CancellationToken cancellationToken = default);
    int CountTotalUsers(RegistrationViewModel model);
}

public record RegistrationResult(bool Success, string? Error, Tenant? Tenant, string? RedirectUrl);

public class RegistrationService : IRegistrationService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IStripePaymentService _stripe;
    private readonly IProvisioningService _provisioning;
    private readonly ITenantContext _tenantContext;

    public RegistrationService(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IStripePaymentService stripe,
        IProvisioningService provisioning,
        ITenantContext tenantContext)
    {
        _db = db;
        _userManager = userManager;
        _stripe = stripe;
        _provisioning = provisioning;
        _tenantContext = tenantContext;
    }

    public int CountTotalUsers(RegistrationViewModel model) =>
        1 + (model.AdditionalUsers?.Count ?? 0);

    public async Task<(RegistrationSubmission Submission, PaymentIntent Intent)> StartRegistrationAsync(
        RegistrationViewModel model, CancellationToken cancellationToken = default)
    {
        var totalUsers = CountTotalUsers(model);
        if (totalUsers > Constants.IncludedSeats)
        {
            throw new InvalidOperationException(
                $"Registration allows at most {Constants.IncludedSeats} users total (admin plus additional users).");
        }

        if (model.AdditionalUsers != null)
        {
            foreach (var user in model.AdditionalUsers)
            {
                if (string.IsNullOrWhiteSpace(user.FullName) || string.IsNullOrWhiteSpace(user.Email))
                {
                    throw new InvalidOperationException("Each additional user requires a full name and email.");
                }
            }
        }

        if (await _userManager.FindByEmailAsync(model.AdminEmail) != null)
        {
            throw new InvalidOperationException("An account with this email already exists.");
        }

        var submission = new RegistrationSubmission
        {
            CompanyName = model.CompanyName.Trim(),
            AdminFullName = model.AdminFullName.Trim(),
            AdminEmail = model.AdminEmail.Trim(),
            AdditionalUsersJson = JsonSerializer.Serialize(model.AdditionalUsers ?? []),
            Status = RegistrationStatus.Submitted,
            SubmittedAt = DateTime.UtcNow
        };

        _db.RegistrationSubmissions.Add(submission);
        await _db.SaveChangesAsync(cancellationToken);

        var intent = await _stripe.CreateValidationPaymentIntentAsync(cancellationToken);

        submission.Status = RegistrationStatus.PaymentPending;
        submission.StripePaymentIntentId = intent.Id;
        await _db.SaveChangesAsync(cancellationToken);

        return (submission, intent);
    }

    public async Task<RegistrationResult> CompleteRegistrationAsync(
        CompleteRegistrationViewModel model, CancellationToken cancellationToken = default)
    {
        var submission = await _db.RegistrationSubmissions
            .FirstOrDefaultAsync(s => s.Id == model.SubmissionId, cancellationToken);

        if (submission == null)
        {
            return new RegistrationResult(false, "Registration not found.", null, null);
        }

        if (submission.Status == RegistrationStatus.Completed && submission.TenantId.HasValue)
        {
            var existing = await _db.Tenants.FindAsync([submission.TenantId.Value], cancellationToken);
            if (existing != null)
            {
                return new RegistrationResult(true, null, existing, $"/{Constants.TenantRoutePrefix}/{existing.Slug}");
            }
        }

        if (submission.StripePaymentIntentId != model.PaymentIntentId)
        {
            return new RegistrationResult(false, "Payment intent mismatch.", null, null);
        }

        PaymentIntent intent;
        try
        {
            intent = await _stripe.GetPaymentIntentAsync(model.PaymentIntentId, cancellationToken);
        }
        catch
        {
            submission.Status = RegistrationStatus.PaymentFailed;
            await _db.SaveChangesAsync(cancellationToken);
            return new RegistrationResult(false, "Unable to verify payment.", null, null);
        }

        if (intent.Status != "succeeded")
        {
            submission.Status = RegistrationStatus.PaymentFailed;
            await _db.SaveChangesAsync(cancellationToken);
            return new RegistrationResult(false, "Payment was not successful. No workspace was created.", null, null);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var slug = SlugHelper.GenerateSlug(submission.CompanyName);
            var tenant = new Tenant
            {
                CompanyName = submission.CompanyName,
                Slug = slug,
                CreatedAt = DateTime.UtcNow,
                Status = TenantStatus.Active,
                Tier = TenantTier.Base,
                SeatAllowance = Constants.IncludedSeats
            };

            _db.Tenants.Add(tenant);
            await _db.SaveChangesAsync(cancellationToken);

            _tenantContext.SetTenant(tenant.Id, tenant.Slug);

            var admin = new ApplicationUser
            {
                UserName = submission.AdminEmail,
                Email = submission.AdminEmail,
                FullName = submission.AdminFullName,
                TenantId = tenant.Id,
                EmailConfirmed = true
            };

            var adminResult = await _userManager.CreateAsync(admin, model.AdminPassword);
            if (!adminResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new RegistrationResult(false, string.Join(", ", adminResult.Errors.Select(e => e.Description)), null, null);
            }

            await _userManager.AddToRoleAsync(admin, Constants.CompanyAdminRole);

            var additionalUsers = JsonSerializer.Deserialize<List<RegistrationUserViewModel>>(submission.AdditionalUsersJson) ?? [];
            foreach (var entry in additionalUsers)
            {
                if (await _userManager.FindByEmailAsync(entry.Email) != null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new RegistrationResult(false, $"Email {entry.Email} is already registered.", null, null);
                }

                var tempPassword = GenerateTemporaryPassword();
                var user = new ApplicationUser
                {
                    UserName = entry.Email.Trim(),
                    Email = entry.Email.Trim(),
                    FullName = entry.FullName.Trim(),
                    TenantId = tenant.Id,
                    EmailConfirmed = true,
                    TemporaryPassword = tempPassword
                };

                var userResult = await _userManager.CreateAsync(user, tempPassword);
                if (!userResult.Succeeded)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new RegistrationResult(false, string.Join(", ", userResult.Errors.Select(e => e.Description)), null, null);
                }

                await _userManager.AddToRoleAsync(user, Constants.UserRole);
            }

            _db.PaymentRecords.Add(new Models.Entities.PaymentRecord
            {
                TenantId = tenant.Id,
                StripePaymentIntentId = intent.Id,
                Amount = (int)intent.Amount,
                Currency = intent.Currency,
                Status = PaymentStatus.Succeeded,
                CreatedAt = DateTime.UtcNow
            });

            await _provisioning.ProvisionWorkspaceAsync(tenant, cancellationToken);

            submission.Status = RegistrationStatus.Completed;
            submission.TenantId = tenant.Id;
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new RegistrationResult(true, null, tenant, $"/{Constants.TenantRoutePrefix}/{tenant.Slug}");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            submission.Status = RegistrationStatus.PaymentFailed;
            await _db.SaveChangesAsync(cancellationToken);
            return new RegistrationResult(false, $"Registration failed: {ex.Message}", null, null);
        }
    }

    private static string GenerateTemporaryPassword()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789!@#$";
        var random = Random.Shared;
        return new string(Enumerable.Range(0, 12).Select(_ => chars[random.Next(chars.Length)]).ToArray());
    }
}
