namespace ShiftPlatform;

/// <summary>
/// Central, documented place for the platform's business constants so the
/// values referenced throughout the code (and the README) never drift apart.
/// </summary>
public static class PlatformConstants
{
    /// <summary>
    /// Seats included for free at registration. Interpretation: the Company
    /// Admin counts as one of these five seats, so a brand-new company may have
    /// the admin plus up to four additional members before needing to buy more.
    /// </summary>
    public const int IncludedSeats = 5;

    /// <summary>Card-validation charge taken at registration: 1.00 AUD.</summary>
    public const long ValidationAmountCents = 100;

    /// <summary>Price of one extra seat under the Pro license: 5.00 AUD.</summary>
    public const long SeatPriceCents = 500;

    /// <summary>Currency used for every Stripe charge in the platform.</summary>
    public const string Currency = "aud";

    /// <summary>
    /// The "upcoming" window shown on dashboards: the next four weeks (28 days).
    /// </summary>
    public const int UpcomingWindowDays = 28;

    public static class Roles
    {
        public const string PlatformOwner = "PlatformOwner";
        public const string CompanyAdmin = "CompanyAdmin";
        public const string User = "User";

        public static readonly string[] All = { PlatformOwner, CompanyAdmin, User };
    }
}
