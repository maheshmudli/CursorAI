namespace ShiftManagementPlatform.ViewModels;

public sealed record ShiftDto(
    int Id,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string RoleLabel,
    string? Notes,
    string? AssignedUserId,
    string? AssignedUserName);

public sealed class ShiftCreateRequest
{
    public DateOnly Date { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string RoleLabel { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public sealed class AssignShiftRequest
{
    public string UserId { get; set; } = string.Empty;
}

public sealed class SwapCreateRequest
{
    public int RequestingAssignmentId { get; set; }
    public int TargetAssignmentId { get; set; }
}
