using System.ComponentModel.DataAnnotations;

namespace ShiftManagementPlatform.ViewModels;

public sealed class RegistrationViewModel : IValidatableObject
{
    [Required, MaxLength(200)]
    public string CompanyName { get; set; } = string.Empty;

    [Required, MaxLength(200), Display(Name = "Admin full name")]
    public string AdminFullName { get; set; } = string.Empty;

    [Required, EmailAddress, Display(Name = "Admin email")]
    public string AdminEmail { get; set; } = string.Empty;

    [Required, MinLength(8), DataType(DataType.Password)]
    public string AdminPassword { get; set; } = string.Empty;

    [Required]
    public string PaymentIntentId { get; set; } = string.Empty;

    public List<RegistrationUserViewModel> Users { get; set; } = new()
    {
        new(), new(), new(), new()
    };

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var additionalUsers = Users.Count(u => !string.IsNullOrWhiteSpace(u.Email));
        if (additionalUsers + 1 > 5)
        {
            yield return new ValidationResult("The base plan includes five total users. The registering admin counts as one seat.", new[] { nameof(Users) });
        }

        foreach (var user in Users.Where(u => !string.IsNullOrWhiteSpace(u.Email)))
        {
            if (string.IsNullOrWhiteSpace(user.FullName))
            {
                yield return new ValidationResult("Each supplied user requires a full name.", new[] { nameof(Users) });
            }
        }
    }
}

public sealed class RegistrationUserViewModel
{
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public sealed record PaymentIntentRequest(int Seats);

public sealed class AddUserViewModel
{
    [Required, MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public sealed class SeatPurchaseViewModel
{
    [Range(1, 100)]
    public int Seats { get; set; } = 1;

    [Required]
    public string PaymentIntentId { get; set; } = string.Empty;
}

public sealed class LoginViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}
