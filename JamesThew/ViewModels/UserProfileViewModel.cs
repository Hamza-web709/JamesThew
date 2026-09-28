using System.ComponentModel.DataAnnotations;

namespace JamesThew.ViewModels;

public class UserProfileViewModel
{
    public string Email { get; set; } = string.Empty;
    public string Roles { get; set; } = string.Empty;
    public string SubscriptionStatus { get; set; } = string.Empty;

    [Required(ErrorMessage = "Display name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Display name must be between 2 and 100 characters.")]
    [Display(Name = "Display Name")]
    public string DisplayName { get; set; } = string.Empty;
}
