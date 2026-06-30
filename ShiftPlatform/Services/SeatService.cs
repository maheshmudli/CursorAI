using Microsoft.EntityFrameworkCore;
using ShiftPlatform.Data;
using ShiftPlatform.Infrastructure;
using ShiftPlatform.Models.Entities;
using ShiftPlatform.Models.Enums;
using Stripe;

namespace ShiftPlatform.Services;

public interface ISeatService
{
    Task<int> GetCurrentUserCountAsync(int tenantId, CancellationToken cancellationToken = default);
    Task<bool> CanAddUserAsync(int tenantId, CancellationToken cancellationToken = default);
    Task<(SeatPurchase Purchase, PaymentIntent Intent)> StartSeatPurchaseAsync(int tenantId, int seatCount, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error)> CompleteSeatPurchaseAsync(int tenantId, int purchaseId, string paymentIntentId, CancellationToken cancellationToken = default);
    Task ApplyWebhookPaymentAsync(string paymentIntentId, string stripeStatus, CancellationToken cancellationToken = default);
}

public class SeatService : ISeatService
{
    private readonly ApplicationDbContext _db;
    private readonly IStripePaymentService _stripe;

    public SeatService(ApplicationDbContext db, IStripePaymentService stripe)
    {
        _db = db;
        _stripe = stripe;
    }

    public async Task<int> GetCurrentUserCountAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        return await _db.Users.CountAsync(u => u.TenantId == tenantId, cancellationToken);
    }

    public async Task<bool> CanAddUserAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await _db.Tenants.FindAsync([tenantId], cancellationToken);
        if (tenant == null) return false;
        var count = await GetCurrentUserCountAsync(tenantId, cancellationToken);
        return count < tenant.SeatAllowance;
    }

    public async Task<(SeatPurchase Purchase, PaymentIntent Intent)> StartSeatPurchaseAsync(
        int tenantId, int seatCount, CancellationToken cancellationToken = default)
    {
        if (seatCount < 1)
        {
            throw new InvalidOperationException("You must purchase at least one seat.");
        }

        var tenant = await _db.Tenants.FindAsync([tenantId], cancellationToken)
            ?? throw new InvalidOperationException("Tenant not found.");

        var intent = await _stripe.CreateSeatPaymentIntentAsync(seatCount, cancellationToken);

        var purchase = new SeatPurchase
        {
            TenantId = tenantId,
            StripePaymentIntentId = intent.Id,
            Amount = (int)intent.Amount,
            Currency = intent.Currency,
            Status = PaymentStatus.Pending,
            SeatsAdded = seatCount,
            CreatedAt = DateTime.UtcNow
        };

        _db.SeatPurchases.Add(purchase);
        await _db.SaveChangesAsync(cancellationToken);

        return (purchase, intent);
    }

    public async Task<(bool Success, string? Error)> CompleteSeatPurchaseAsync(
        int tenantId, int purchaseId, string paymentIntentId, CancellationToken cancellationToken = default)
    {
        var purchase = await _db.SeatPurchases
            .FirstOrDefaultAsync(p => p.Id == purchaseId && p.TenantId == tenantId, cancellationToken);

        if (purchase == null)
        {
            return (false, "Seat purchase not found.");
        }

        if (purchase.Status == PaymentStatus.Succeeded)
        {
            return (true, null);
        }

        if (purchase.StripePaymentIntentId != paymentIntentId)
        {
            return (false, "Payment intent mismatch.");
        }

        PaymentIntent intent;
        try
        {
            intent = await _stripe.GetPaymentIntentAsync(paymentIntentId, cancellationToken);
        }
        catch
        {
            return (false, "Unable to verify payment.");
        }

        if (intent.Status != "succeeded")
        {
            purchase.Status = PaymentStatus.Failed;
            await _db.SaveChangesAsync(cancellationToken);
            return (false, "Payment was not successful.");
        }

        await ApplySeatPurchaseAsync(purchase, cancellationToken);
        return (true, null);
    }

    public async Task ApplyWebhookPaymentAsync(string paymentIntentId, string stripeStatus, CancellationToken cancellationToken = default)
    {
        var paymentStatus = _stripe.MapStripeStatus(stripeStatus);

        var paymentRecord = await _db.PaymentRecords
            .FirstOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntentId, cancellationToken);
        if (paymentRecord != null)
        {
            paymentRecord.Status = paymentStatus;
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        var seatPurchase = await _db.SeatPurchases
            .FirstOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntentId, cancellationToken);
        if (seatPurchase != null && paymentStatus == PaymentStatus.Succeeded && seatPurchase.Status != PaymentStatus.Succeeded)
        {
            await ApplySeatPurchaseAsync(seatPurchase, cancellationToken);
        }
        else if (seatPurchase != null)
        {
            seatPurchase.Status = paymentStatus;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task ApplySeatPurchaseAsync(SeatPurchase purchase, CancellationToken cancellationToken)
    {
        var tenant = await _db.Tenants.FindAsync([purchase.TenantId], cancellationToken);
        if (tenant == null) return;

        purchase.Status = PaymentStatus.Succeeded;
        tenant.SeatAllowance += purchase.SeatsAdded;
        if (tenant.Tier == TenantTier.Base)
        {
            tenant.Tier = TenantTier.Pro;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
