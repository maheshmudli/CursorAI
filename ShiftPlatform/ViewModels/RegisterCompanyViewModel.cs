using System.ComponentModel.DataAnnotations;

namespace ShiftPlatform.ViewModels;

public class RegisterCompanyViewModel
{
    [Required]
    [StringLength(200)]
    public string CompanyName { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string AdminFullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string AdminEmail { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [MinLength(8)]
    public string AdminPassword { get; set; } = string.Empty;

    public List<RegisterUserInput> Users { get; set; } = new();

    [Required]
    public string StripePaymentMethodId { get; set; } = string.Empty;
}

public class RegisterUserInput
{
    [Required]
    [StringLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
