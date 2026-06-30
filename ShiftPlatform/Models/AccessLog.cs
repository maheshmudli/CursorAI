using ShiftPlatform.Models.Interfaces;

namespace ShiftPlatform.Models;

public class AccessLog : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime? LastLoginAt { get; set; }

    public ApplicationUser? User { get; set; }
}
