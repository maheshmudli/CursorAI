namespace ShiftPlatform.Infrastructure;

public interface ITenantContext
{
    int? TenantId { get; }
    string? TenantSlug { get; }
    bool HasTenant { get; }
    void SetTenant(int tenantId, string slug);
    void Clear();
}

public class TenantContext : ITenantContext
{
    public int? TenantId { get; private set; }
    public string? TenantSlug { get; private set; }
    public bool HasTenant => TenantId.HasValue;

    public void SetTenant(int tenantId, string slug)
    {
        TenantId = tenantId;
        TenantSlug = slug;
    }

    public void Clear()
    {
        TenantId = null;
        TenantSlug = null;
    }
}
