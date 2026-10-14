using System.ComponentModel.DataAnnotations;

namespace JamesThew.ViewModels;

public class ForgotPasswordViewModel
{
    private string email = string.Empty;

    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(256)]
    [Display(Name = "Email Address")]
    public string Email
    {
        get => email;
        set => email = value?.Trim() ?? string.Empty;
    }
}
