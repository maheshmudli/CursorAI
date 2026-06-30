namespace ShiftPlatform.Models.ViewModels;

public class PlatformDashboardViewModel
{
    public List<RegistrationRow> Registrations { get; set; } = [];
    public List<WorkspaceRow> Workspaces { get; set; } = [];
    public List<PaymentRow> ValidationPayments { get; set; } = [];
    public List<TenantSummaryRow> TenantSummaries { get; set; } = [];
    public List<AccessRow> AccessRecords { get; set; } = [];
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
}

public class RegistrationRow
{
    public int Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string AdminEmail { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
}

public class WorkspaceRow
{
    public int Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public DateTime ProvisionedAt { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class PaymentRow
{
    public int Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public int Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string StripePaymentIntentId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class TenantSummaryRow
{
    public int TenantId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Tier { get; set; } = string.Empty;
    public int SeatAllowance { get; set; }
    public int UserCount { get; set; }
    public List<SeatPurchaseRow> SeatPurchases { get; set; } = [];
}

public class SeatPurchaseRow
{
    public int Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string StripePaymentIntentId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int SeatsAdded { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AccessRow
{
    public string CompanyName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime LastLoginAt { get; set; }
}
