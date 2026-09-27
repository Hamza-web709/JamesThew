using System.ComponentModel.DataAnnotations;

namespace JamesThew.ViewModels;

// Allow-list registration input: clients cannot bind roles or activation state.
public class RegisterViewModel
{
    private string displayName = string.Empty;
    private string email = string.Empty;

    [Required, StringLength(100, MinimumLength = 2), Display(Name = "Display name")]
    public string DisplayName { get => displayName; set => displayName = value?.Trim() ?? string.Empty; }

    [Required, EmailAddress, StringLength(256)]
    public string Email { get => email; set => email = value?.Trim() ?? string.Empty; }

    [Required, StringLength(128, MinimumLength = 12), DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Compare(nameof(Password)), Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
