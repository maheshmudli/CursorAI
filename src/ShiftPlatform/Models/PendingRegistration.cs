namespace ShiftPlatform.Models;

/// <summary>
/// Holds a submitted registration between the form post and a successful
/// validation payment. Nothing in the tenant graph is created until the
/// associated Payment Intent succeeds, so this acts as the staging area and a
/// permanent record of every registration form submitted (with its status).
/// </summary>
public class PendingRegistration
{
    public int Id { get; set; }

    /// <summary>Opaque token used in the payment-step URL.</summary>
    public Guid Token { get; set; } = Guid.NewGuid();

    public string CompanyName { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;

    /// <summary>JSON-serialised <see cref="ViewModels.RegistrationInput"/>.</summary>
    public string PayloadJson { get; set; } = string.Empty;

    public string StripePaymentIntentId { get; set; } = string.Empty;
    public long Amount { get; set; }
    public string Currency { get; set; } = PlatformConstants.Currency;

    /// <summary>Submitted | Paid | Provisioned | Failed | Abandoned.</summary>
    public string Status { get; set; } = "Submitted";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Set once provisioning completes, linking to the created tenant.</summary>
    public int? TenantId { get; set; }
}
