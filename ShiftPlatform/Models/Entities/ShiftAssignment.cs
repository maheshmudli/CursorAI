namespace ShiftPlatform.Models.Entities;

public class ShiftAssignment : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int ShiftId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public Shift Shift { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
}
