using ShiftManagementPlatform.Models;

namespace ShiftManagementPlatform.ViewModels;

public sealed record PlatformDashboardViewModel(
    IReadOnlyList<RegistrationSubmission> Registrations,
    IReadOnlyList<Tenant> Tenants,
    IReadOnlyList<ProvisioningRecord> ProvisioningRecords,
    IReadOnlyList<PaymentRecord> Payments,
    IReadOnlyList<SeatPurchase> SeatPurchases,
    IReadOnlyList<AccessLog> AccessLogs,
    IReadOnlyDictionary<int, int> UserCounts);
