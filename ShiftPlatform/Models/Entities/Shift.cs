namespace ShiftPlatform.Models.Entities;

public class Shift : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public DateOnly Date { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string RoleLabel { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public ShiftAssignment? Assignment { get; set; }
}
