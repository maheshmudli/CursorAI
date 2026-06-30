namespace ShiftManager.Services
{
    public class TenantContext : ITenantContext
    {
        public int? CurrentTenantId { get; set; }
        public string? CurrentTenantSlug { get; set; }
    }
}
