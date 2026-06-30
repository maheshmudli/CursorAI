using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShiftManager.ViewModels
{
    public class UserEntryViewModel
    {
        [Required(ErrorMessage = "Full name is required")]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;
    }

    public class RegistrationViewModel
    {
        [Required(ErrorMessage = "Company name is required")]
        [StringLength(100, ErrorMessage = "Company name cannot exceed 100 characters")]
        [Display(Name = "Company Name")]
        public string CompanyName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Full name is required")]
        [Display(Name = "Your Full Name")]
        public string AdminFullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        [Display(Name = "Your Email")]
        public string AdminEmail { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string AdminPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirm password is required")]
        [DataType(DataType.Password)]
        [Compare("AdminPassword", ErrorMessage = "Passwords do not match")]
        [Display(Name = "Confirm Password")]
        public string AdminPasswordConfirm { get; set; } = string.Empty;

        public List<UserEntryViewModel> AdditionalUsers { get; set; } = new();

        // Stripe payment method ID from Elements
        public string? PaymentMethodId { get; set; }
        // Payment Intent ID for tracking
        public string? PaymentIntentId { get; set; }
    }

    public class RegistrationResultViewModel
    {
        public string CompanyName { get; set; } = string.Empty;
        public string TenantSlug { get; set; } = string.Empty;
        public string WorkspaceUrl { get; set; } = string.Empty;
        public List<UserCredential> UserCredentials { get; set; } = new();
    }

    public class UserCredential
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string TemporaryPassword { get; set; } = string.Empty;
    }

    public class LoginViewModel
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
        public string? ReturnUrl { get; set; }
    }
}
