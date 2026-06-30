namespace ShiftPlatform.Services;

/// <summary>
/// Scoped holder for the tenant resolved from the current request's route. The
/// DbContext reads <see cref="TenantId"/> to power its global query filter, so
/// tenant isolation cannot be bypassed by a forgotten <c>Where</c> clause.
/// </summary>
public interface ITenantContext
{
    int? TenantId { get; }
    string? Slug { get; }
    bool HasTenant { get; }

    void SetTenant(int tenantId, string slug);
}

public class TenantContext : ITenantContext
{
    public int? TenantId { get; private set; }
    public string? Slug { get; private set; }
    public bool HasTenant => TenantId.HasValue;

    public void SetTenant(int tenantId, string slug)
    {
        TenantId = tenantId;
        Slug = slug;
    }
}
