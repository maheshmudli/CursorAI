using Stripe;

namespace ShiftManagementPlatform.Services;

public sealed class PaymentService
{
    private readonly IConfiguration _configuration;

    public PaymentService(IConfiguration configuration)
    {
        _configuration = configuration;
        StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];
    }

    public async Task<PaymentIntent> CreatePaymentIntentAsync(long amount, string currency, string description, int seats = 0)
    {
        var service = new PaymentIntentService();
        return await service.CreateAsync(new PaymentIntentCreateOptions
        {
            Amount = amount,
            Currency = currency,
            Description = description,
            AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
            {
                Enabled = true
            },
            Metadata = new Dictionary<string, string>
            {
                ["seats"] = seats.ToString()
            }
        });
    }

    public async Task<PaymentIntent> GetPaymentIntentAsync(string paymentIntentId)
    {
        var service = new PaymentIntentService();
        return await service.GetAsync(paymentIntentId);
    }

    public bool IsSucceeded(PaymentIntent intent) =>
        string.Equals(intent.Status, "succeeded", StringComparison.OrdinalIgnoreCase);
}
