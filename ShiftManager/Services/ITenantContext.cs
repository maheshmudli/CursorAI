namespace ShiftManager.Services
{
    public interface ITenantContext
    {
        int? CurrentTenantId { get; set; }
        string? CurrentTenantSlug { get; set; }
    }
}
