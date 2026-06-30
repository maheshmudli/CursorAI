using System.ComponentModel.DataAnnotations;

namespace ShiftPlatform.ViewModels;

public class ShiftDto
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string RoleLabel { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public int? AssignmentId { get; set; }
    public string? AssignedUserId { get; set; }
    public string? AssignedUserName { get; set; }
}

public class ShiftCreateDto
{
    [Required] public DateOnly Date { get; set; }
    [Required] public TimeOnly StartTime { get; set; }
    [Required] public TimeOnly EndTime { get; set; }
    [Required, MaxLength(100)] public string RoleLabel { get; set; } = string.Empty;
    [MaxLength(1000)] public string? Notes { get; set; }
}

public class AssignDto
{
    [Required] public string UserId { get; set; } = string.Empty;
}

public class UserDto
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public class UserCreateDto
{
    [Required, MaxLength(200)] public string FullName { get; set; } = string.Empty;
    [Required, EmailAddress, MaxLength(256)] public string Email { get; set; } = string.Empty;
}

public class SwapCreateDto
{
    [Required] public int RequestingAssignmentId { get; set; }
    [Required] public int TargetAssignmentId { get; set; }
}

public class SwapDto
{
    public int Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public string RequestingUserName { get; set; } = string.Empty;
    public string TargetUserName { get; set; } = string.Empty;
    public ShiftDto? RequestingShift { get; set; }
    public ShiftDto? TargetShift { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public bool CanApprove { get; set; }
}

public class DashboardDto
{
    public bool IsAdmin { get; set; }
    public int UpcomingWindowDays { get; set; }
    public List<ShiftDto> MyShifts { get; set; } = new();
    public List<ShiftDto> Upcoming { get; set; } = new();
}
