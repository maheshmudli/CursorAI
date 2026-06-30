namespace ShiftManagementPlatform.Services;

public interface ITenantProvider
{
    int? TenantId { get; }
    string? TenantSlug { get; }
    void SetTenant(int tenantId, string tenantSlug);
}

public sealed class TenantProvider : ITenantProvider
{
    public int? TenantId { get; private set; }
    public string? TenantSlug { get; private set; }

    public void SetTenant(int tenantId, string tenantSlug)
    {
        TenantId = tenantId;
        TenantSlug = tenantSlug;
    }
}
