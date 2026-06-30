using ShiftPlatform.Models;

namespace ShiftPlatform.ViewModels;

public class TenantOverviewRow
{
    public int TenantId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Route => $"/t/{Slug}";
    public TenantTier Tier { get; set; }
    public TenantStatus Status { get; set; }
    public int SeatAllowance { get; set; }
    public int UserCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public int SeatPurchaseCount { get; set; }
}

public class ProvisioningRecordRow
{
    public string CompanyName { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime ProvisionedAt { get; set; }
}

public class AccessRecordRow
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
    public DateTime? LastLoginAt { get; set; }
}

public class SeatPurchaseRow
{
    public string CompanyName { get; set; } = string.Empty;
    public string StripePaymentIntentId { get; set; } = string.Empty;
    public long Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int SeatsAdded { get; set; }
    public bool Applied { get; set; }
    public DateTime CreatedAt { get; set; }
    public string AmountDisplay => $"{Amount / 100m:0.00} {Currency.ToUpperInvariant()}";
}

public class RegistrationRow
{
    public string CompanyName { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StripePaymentIntentId { get; set; } = string.Empty;
    public long Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string AmountDisplay => $"{Amount / 100m:0.00} {Currency.ToUpperInvariant()}";
}

public class PaymentRow
{
    public string CompanyName { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string StripePaymentIntentId { get; set; } = string.Empty;
    public long Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string AmountDisplay => $"{Amount / 100m:0.00} {Currency.ToUpperInvariant()}";
}
