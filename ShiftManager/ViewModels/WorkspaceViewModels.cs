using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShiftManager.ViewModels
{
    public class ShiftViewModel
    {
        public int Id { get; set; }
        [Required, Display(Name = "Date")]
        public DateTime Date { get; set; }
        [Required, Display(Name = "Start Time")]
        public string StartTime { get; set; } = "09:00";
        [Required, Display(Name = "End Time")]
        public string EndTime { get; set; } = "17:00";
        [Required, StringLength(100), Display(Name = "Role / Label")]
        public string RoleLabel { get; set; } = string.Empty;
        [StringLength(500)]
        public string? Notes { get; set; }
        public string? AssignedUserId { get; set; }
        public string? AssignedUserName { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
    }

    public class CreateShiftViewModel
    {
        [Required, Display(Name = "Date")]
        public DateTime Date { get; set; } = DateTime.UtcNow.Date.AddDays(1);
        [Required, Display(Name = "Start Time (HH:mm)")]
        public string StartTime { get; set; } = "09:00";
        [Required, Display(Name = "End Time (HH:mm)")]
        public string EndTime { get; set; } = "17:00";
        [Required, StringLength(100), Display(Name = "Role / Label")]
        public string RoleLabel { get; set; } = string.Empty;
        [StringLength(500)]
        public string? Notes { get; set; }
    }

    public class AssignShiftViewModel
    {
        [Required]
        public string UserId { get; set; } = string.Empty;
    }

    public class SwapRequestViewModel
    {
        public int Id { get; set; }
        public string RequestingUserName { get; set; } = string.Empty;
        public string TargetUserName { get; set; } = string.Empty;
        public DateTime RequestingShiftDate { get; set; }
        public string RequestingShiftRole { get; set; } = string.Empty;
        public DateTime TargetShiftDate { get; set; }
        public string TargetShiftRole { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class CreateSwapRequestViewModel
    {
        [Required]
        public int MyAssignmentId { get; set; }
        [Required]
        public int TargetAssignmentId { get; set; }
    }

    public class DashboardViewModel
    {
        public List<ShiftViewModel> MyUpcomingShifts { get; set; } = new();
        public List<ShiftViewModel> AllUpcomingShifts { get; set; } = new();
        public List<SwapRequestViewModel> PendingSwaps { get; set; } = new();
        public bool IsAdmin { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public DateTime WindowStart { get; set; }
        public DateTime WindowEnd { get; set; }
    }

    public class TenantUserViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime? LastLogin { get; set; }
    }

    public class AddUserViewModel
    {
        [Required]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        public string? PaymentMethodId { get; set; }
        public string? PaymentIntentId { get; set; }
        public bool NeedsSeatPurchase { get; set; }
    }

    public class SeatUpgradeViewModel
    {
        public int CurrentSeatAllowance { get; set; }
        public int CurrentUserCount { get; set; }
        public int SeatsToAdd { get; set; } = 1;
        public long CostPerSeat { get; set; } = 500;
        public string? PaymentMethodId { get; set; }
        public string? PaymentIntentId { get; set; }
        public string? PendingUserFullName { get; set; }
        public string? PendingUserEmail { get; set; }
    }
}
