namespace ShiftPlatform.Models.Entities;

public class AccessLog
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime LastLoginAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
}
