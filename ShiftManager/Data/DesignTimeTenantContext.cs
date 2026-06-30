using ShiftManager.Services;

namespace ShiftManager.Data
{
    /// <summary>
    /// Used only during design-time migrations; returns null for all filters so no tenant isolation is applied.
    /// </summary>
    public class DesignTimeTenantContext : ITenantContext
    {
        public int? CurrentTenantId { get; set; } = null;
        public string? CurrentTenantSlug { get; set; } = null;
    }
}
