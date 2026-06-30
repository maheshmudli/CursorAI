using ShiftPlatform.Models.Interfaces;

namespace ShiftPlatform.Models;

public class ShiftAssignment : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ShiftId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    public Shift? Shift { get; set; }
    public ApplicationUser? User { get; set; }
}
