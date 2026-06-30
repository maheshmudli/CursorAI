using System.ComponentModel.DataAnnotations;

namespace ShiftPlatform.Models.ViewModels;

public class RegistrationUserViewModel
{
    [Required]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public class RegistrationViewModel
{
    [Required]
    [Display(Name = "Company Name")]
    public string CompanyName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Your Full Name")]
    public string AdminFullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [Display(Name = "Your Email")]
    public string AdminEmail { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [MinLength(8)]
  public string AdminPassword { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(AdminPassword))]
    [Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public List<RegistrationUserViewModel> AdditionalUsers { get; set; } = [];
}

public class CompleteRegistrationViewModel
{
    public int SubmissionId { get; set; }
    public string PaymentIntentId { get; set; } = string.Empty;
    public string AdminPassword { get; set; } = string.Empty;
}

public class LoginViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}

public class AddUserViewModel
{
    [Required]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public class BuySeatsViewModel
{
    [Range(1, 20)]
    public int SeatCount { get; set; } = 1;
}

public class CompleteSeatPurchaseViewModel
{
    public int PurchaseId { get; set; }
    public string PaymentIntentId { get; set; } = string.Empty;
}
