using System.ComponentModel.DataAnnotations;

namespace ShiftPlatform.ViewModels;

public class NewUserInput
{
    [Required, MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;
}

/// <summary>
/// The company registration form. The admin is captured separately from the
/// additional members; the admin counts as one of the five included seats, so
/// at most four additional members may be supplied here.
/// </summary>
public class RegistrationInput
{
    [Required, MaxLength(200), Display(Name = "Company name")]
    public string CompanyName { get; set; } = string.Empty;

    [Required, MaxLength(200), Display(Name = "Your full name")]
    public string AdminFullName { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(256), Display(Name = "Your email")]
    public string AdminEmail { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), MinLength(8), Display(Name = "Password")]
    public string AdminPassword { get; set; } = string.Empty;

    [DataType(DataType.Password), Display(Name = "Confirm password")]
    [Compare(nameof(AdminPassword), ErrorMessage = "The passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    /// <summary>Additional members to create immediately (besides the admin).</summary>
    public List<NewUserInput> Members { get; set; } = new();
}
