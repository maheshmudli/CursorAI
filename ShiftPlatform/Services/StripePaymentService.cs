using Microsoft.Extensions.Options;
using ShiftPlatform.Infrastructure;
using ShiftPlatform.Models.Enums;
using Stripe;

namespace ShiftPlatform.Services;

public class StripeSettings
{
    public string SecretKey { get; set; } = string.Empty;
    public string PublishableKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
}

public interface IStripePaymentService
{
    Task<PaymentIntent> CreateValidationPaymentIntentAsync(CancellationToken cancellationToken = default);
    Task<PaymentIntent> CreateSeatPaymentIntentAsync(int seatCount, CancellationToken cancellationToken = default);
    Task<PaymentIntent> GetPaymentIntentAsync(string paymentIntentId, CancellationToken cancellationToken = default);
    Models.Enums.PaymentStatus MapStripeStatus(string status);
}

public class StripePaymentService : IStripePaymentService
{
    public StripePaymentService(IOptions<StripeSettings> options)
    {
        StripeConfiguration.ApiKey = options.Value.SecretKey;
    }

    public async Task<PaymentIntent> CreateValidationPaymentIntentAsync(CancellationToken cancellationToken = default)
    {
        var service = new PaymentIntentService();
        return await service.CreateAsync(new PaymentIntentCreateOptions
        {
            Amount = Constants.ValidationAmountCents,
            Currency = Constants.Currency,
            CaptureMethod = "automatic",
            Metadata = new Dictionary<string, string> { ["type"] = "validation" }
        }, cancellationToken: cancellationToken);
    }

    public async Task<PaymentIntent> CreateSeatPaymentIntentAsync(int seatCount, CancellationToken cancellationToken = default)
    {
        var service = new PaymentIntentService();
        return await service.CreateAsync(new PaymentIntentCreateOptions
        {
            Amount = Constants.SeatPriceCents * seatCount,
            Currency = Constants.Currency,
            CaptureMethod = "automatic",
            Metadata = new Dictionary<string, string>
            {
                ["type"] = "seat_purchase",
                ["seats"] = seatCount.ToString()
            }
        }, cancellationToken: cancellationToken);
    }

    public async Task<PaymentIntent> GetPaymentIntentAsync(string paymentIntentId, CancellationToken cancellationToken = default)
    {
        var service = new PaymentIntentService();
        return await service.GetAsync(paymentIntentId, cancellationToken: cancellationToken);
    }

    public Models.Enums.PaymentStatus MapStripeStatus(string status) => status switch
    {
        "succeeded" => Models.Enums.PaymentStatus.Succeeded,
        "canceled" => Models.Enums.PaymentStatus.Canceled,
        "processing" => Models.Enums.PaymentStatus.Pending,
        _ => Models.Enums.PaymentStatus.Failed
    };
}
