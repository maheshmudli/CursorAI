using System.ComponentModel.DataAnnotations;

namespace ShiftPlatform.Models;

/// <summary>A schedulable shift owned by a single tenant.</summary>
public class Shift
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public DateOnly Date { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    [Required, MaxLength(100)]
    public string RoleLabel { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    /// <summary>Id of the user (admin) who created the shift.</summary>
    public string CreatedBy { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ShiftAssignment? Assignment { get; set; }
}
