namespace ShiftPlatform.Models.ViewModels;

public class ShiftDto
{
    public int Id { get; set; }
    public string Date { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public string RoleLabel { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? AssignedUserId { get; set; }
    public string? AssignedUserName { get; set; }
    public int? AssignmentId { get; set; }
}

public class CreateShiftRequest
{
    public string Date { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public string RoleLabel { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class UpdateShiftRequest
{
    public string Date { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public string RoleLabel { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class AssignShiftRequest
{
    public string UserId { get; set; } = string.Empty;
}

public class UserDto
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public class SwapRequestDto
{
    public int Id { get; set; }
    public string RequestingUserName { get; set; } = string.Empty;
    public string TargetUserName { get; set; } = string.Empty;
    public string RequestingShiftLabel { get; set; } = string.Empty;
    public string TargetShiftLabel { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class CreateSwapRequest
{
    public int RequestingAssignmentId { get; set; }
    public int TargetAssignmentId { get; set; }
}

public class DashboardDto
{
    public List<ShiftDto> AssignedShifts { get; set; } = [];
    public List<ShiftDto> UpcomingShifts { get; set; } = [];
    public int UpcomingWeeks { get; set; }
}

public class ApiError
{
    public string Message { get; set; } = string.Empty;
}
