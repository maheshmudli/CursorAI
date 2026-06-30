namespace ShiftPlatform.Services;

public interface ITenantContext
{
    Guid? CurrentTenantId { get; }
    string? CurrentTenantSlug { get; }
    bool IsTenantRoute { get; }
    void SetTenant(Guid tenantId, string slug);
    void Clear();
}

public class TenantContext : ITenantContext
{
    public Guid? CurrentTenantId { get; private set; }
    public string? CurrentTenantSlug { get; private set; }
    public bool IsTenantRoute => CurrentTenantId.HasValue;

    public void SetTenant(Guid tenantId, string slug)
    {
        CurrentTenantId = tenantId;
        CurrentTenantSlug = slug;
    }

    public void Clear()
    {
        CurrentTenantId = null;
        CurrentTenantSlug = null;
    }
}
