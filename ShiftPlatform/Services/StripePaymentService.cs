using Microsoft.Extensions.Options;
using ShiftPlatform.Options;
using Stripe;

namespace ShiftPlatform.Services;

public interface IStripePaymentService
{
    Task<PaymentIntent> CreateAndConfirmPaymentIntentAsync(long amount, string currency, string paymentMethodId, Dictionary<string, string>? metadata = null);
}

public class StripePaymentService : IStripePaymentService
{
    private readonly PaymentIntentService _paymentIntentService = new();
    private readonly StripeOptions _stripeOptions;

    public StripePaymentService(IOptions<StripeOptions> stripeOptions)
    {
        _stripeOptions = stripeOptions.Value;
        if (_stripeOptions.EnablePayments)
        {
            StripeConfiguration.ApiKey = _stripeOptions.SecretKey;
        }
    }

    public async Task<PaymentIntent> CreateAndConfirmPaymentIntentAsync(long amount, string currency, string paymentMethodId, Dictionary<string, string>? metadata = null)
    {
        if (!_stripeOptions.EnablePayments)
        {
            return new PaymentIntent
            {
                Id = $"disabled_{Guid.NewGuid():N}",
                Amount = amount,
                Currency = currency,
                Status = "succeeded",
                Metadata = metadata
            };
        }

        var createOptions = new PaymentIntentCreateOptions
        {
            Amount = amount,
            Currency = currency,
            PaymentMethod = paymentMethodId,
            Confirm = true,
            AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
            {
                Enabled = true,
                AllowRedirects = "never"
            },
            Metadata = metadata
        };

        return await _paymentIntentService.CreateAsync(createOptions);
    }
}
