using ShiftPlatform.Models.Interfaces;

namespace ShiftPlatform.Models;

public class Shift : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public DateOnly Date { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string RoleLabel { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant? Tenant { get; set; }
    public ICollection<ShiftAssignment> Assignments { get; set; } = new List<ShiftAssignment>();
}
