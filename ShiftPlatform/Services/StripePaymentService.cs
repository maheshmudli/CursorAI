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

    public StripePaymentService(IOptions<StripeOptions> stripeOptions)
    {
        StripeConfiguration.ApiKey = stripeOptions.Value.SecretKey;
    }

    public async Task<PaymentIntent> CreateAndConfirmPaymentIntentAsync(long amount, string currency, string paymentMethodId, Dictionary<string, string>? metadata = null)
    {
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
