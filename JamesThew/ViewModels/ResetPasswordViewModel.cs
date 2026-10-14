using System.ComponentModel.DataAnnotations;

namespace JamesThew.ViewModels;

public class ResetPasswordViewModel
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "New password is required.")]
    [StringLength(128, MinimumLength = 12, ErrorMessage = "Password must be at least 12 characters.")]
    [DataType(DataType.Password)]
    [Display(Name = "New Password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirm new password is required.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm New Password")]
    [Compare(nameof(NewPassword), ErrorMessage = "The password and confirmation password do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
