using Microsoft.EntityFrameworkCore;
using ShiftPlatform.Data;
using ShiftPlatform.Models;

namespace ShiftPlatform.Services;

public interface ISeatService
{
    /// <summary>Creates a Stripe Payment Intent for <paramref name="seats"/> extra seats and records a pending purchase.</summary>
    Task<(SeatPurchase Purchase, PaymentIntentResult Intent)> CreateSeatPurchaseAsync(int tenantId, int seats);

    /// <summary>
    /// Applies a seat purchase once its payment has succeeded: bumps the tenant's
    /// allowance and Pro tier exactly once (idempotent for webhook replays).
    /// </summary>
    Task<bool> ApplySeatPurchaseAsync(string paymentIntentId, string status);
}

public class SeatService : ISeatService
{
    private readonly ApplicationDbContext _db;
    private readonly IPaymentGateway _gateway;
    private readonly ILogger<SeatService> _logger;

    public SeatService(ApplicationDbContext db, IPaymentGateway gateway, ILogger<SeatService> logger)
    {
        _db = db;
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<(SeatPurchase, PaymentIntentResult)> CreateSeatPurchaseAsync(int tenantId, int seats)
    {
        if (seats < 1) throw new ArgumentOutOfRangeException(nameof(seats), "At least one seat must be purchased.");

        var amount = PlatformConstants.SeatPriceCents * seats;
        var intent = await _gateway.CreatePaymentIntentAsync(amount, PlatformConstants.Currency,
            new Dictionary<string, string>
            {
                ["purpose"] = "seat_purchase",
                ["tenantId"] = tenantId.ToString(),
                ["seats"] = seats.ToString()
            });

        var purchase = new SeatPurchase
        {
            TenantId = tenantId,
            StripePaymentIntentId = intent.Id,
            Amount = amount,
            Currency = PlatformConstants.Currency,
            Status = intent.Status,
            SeatsAdded = seats,
            Applied = false
        };
        _db.SeatPurchases.Add(purchase);
        await _db.SaveChangesAsync();

        return (purchase, intent);
    }

    public async Task<bool> ApplySeatPurchaseAsync(string paymentIntentId, string status)
    {
        var purchase = await _db.SeatPurchases.FirstOrDefaultAsync(s => s.StripePaymentIntentId == paymentIntentId);
        if (purchase == null) return false;

        purchase.Status = status;

        if (status == "succeeded" && !purchase.Applied)
        {
            var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == purchase.TenantId);
            if (tenant != null)
            {
                tenant.SeatAllowance += purchase.SeatsAdded;
                tenant.Tier = TenantTier.Pro;
                purchase.Applied = true;
                _logger.LogInformation("Applied {Seats} seat(s) to tenant {TenantId}; new allowance {Allowance}.",
                    purchase.SeatsAdded, tenant.Id, tenant.SeatAllowance);
            }
        }

        await _db.SaveChangesAsync();
        return purchase.Applied;
    }
}
