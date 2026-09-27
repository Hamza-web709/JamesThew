using System.ComponentModel.DataAnnotations;

namespace JamesThew.ViewModels;

public class LoginViewModel
{
    private string email = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get => email; set => email = value?.Trim() ?? string.Empty; }

    [Required, StringLength(128), DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}
