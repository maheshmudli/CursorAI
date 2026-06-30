using Microsoft.Extensions.Configuration;
using Stripe;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShiftManager.Services
{
    public interface IStripeService
    {
        Task<PaymentIntent> CreatePaymentIntentAsync(long amount, string currency, Dictionary<string, string>? metadata = null);
        Task<PaymentIntent> ConfirmPaymentIntentAsync(string paymentIntentId, string paymentMethodId);
        Task<PaymentIntent> RetrievePaymentIntentAsync(string paymentIntentId);
    }

    public class StripeService : IStripeService
    {
        private readonly string _secretKey;

        public StripeService(IConfiguration configuration)
        {
            _secretKey = configuration["Stripe:SecretKey"] ?? throw new System.InvalidOperationException("Stripe:SecretKey not configured");
            StripeConfiguration.ApiKey = _secretKey;
        }

        public async Task<PaymentIntent> CreatePaymentIntentAsync(long amount, string currency, Dictionary<string, string>? metadata = null)
        {
            StripeConfiguration.ApiKey = _secretKey;
            var options = new PaymentIntentCreateOptions
            {
                Amount = amount,
                Currency = currency,
                AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
                {
                    Enabled = true,
                },
                Metadata = metadata
            };
            var service = new PaymentIntentService();
            return await service.CreateAsync(options);
        }

        public async Task<PaymentIntent> ConfirmPaymentIntentAsync(string paymentIntentId, string paymentMethodId)
        {
            StripeConfiguration.ApiKey = _secretKey;
            var options = new PaymentIntentConfirmOptions
            {
                PaymentMethod = paymentMethodId,
                ReturnUrl = "https://example.com/return"
            };
            var service = new PaymentIntentService();
            return await service.ConfirmAsync(paymentIntentId, options);
        }

        public async Task<PaymentIntent> RetrievePaymentIntentAsync(string paymentIntentId)
        {
            StripeConfiguration.ApiKey = _secretKey;
            var service = new PaymentIntentService();
            return await service.GetAsync(paymentIntentId);
        }
    }
}
