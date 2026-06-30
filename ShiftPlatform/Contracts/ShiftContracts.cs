using System.ComponentModel.DataAnnotations;

namespace ShiftPlatform.Contracts;

public class CreateShiftRequest
{
    [Required]
    public DateOnly Date { get; set; }

    [Required]
    public TimeOnly StartTime { get; set; }

    [Required]
    public TimeOnly EndTime { get; set; }

    [Required]
    [StringLength(100)]
    public string RoleLabel { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Notes { get; set; }
}

public class UpdateShiftRequest : CreateShiftRequest
{
}

public class AssignShiftRequest
{
    [Required]
    public string UserId { get; set; } = string.Empty;
}

public class CreateSwapRequest
{
    [Required]
    public Guid RequestingAssignmentId { get; set; }

    [Required]
    public string TargetUserId { get; set; } = string.Empty;

    [Required]
    public Guid TargetAssignmentId { get; set; }
}

public class PurchaseSeatsRequest
{
    [Range(1, 100)]
    public int SeatCount { get; set; } = 1;

    [Required]
    public string StripePaymentMethodId { get; set; } = string.Empty;
}

public class CreateUserRequest
{
    [Required]
    [StringLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
