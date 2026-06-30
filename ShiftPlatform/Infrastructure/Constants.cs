namespace ShiftPlatform.Infrastructure;

public static class Constants
{
    public const string PlatformOwnerRole = "PlatformOwner";
    public const string CompanyAdminRole = "CompanyAdmin";
    public const string UserRole = "User";

    public const int IncludedSeats = 5;
    public const int ValidationAmountCents = 100;
    public const int SeatPriceCents = 500;
    public const string Currency = "aud";

    /// <summary>Upcoming shifts are shown for the next four weeks.</summary>
    public const int UpcomingWeeks = 4;

    public const string TenantRoutePrefix = "t";
}
