using System.ComponentModel.DataAnnotations;

namespace JamesThew.ViewModels;

public class VerifyResetOtpViewModel
{
    private string email = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email
    {
        get => email;
        set => email = value?.Trim() ?? string.Empty;
    }

    public string MaskedEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter the six-digit verification code.")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "Verification code must be exactly six digits.")]
    public string Code { get; set; } = string.Empty;

    public int ResendCooldownSeconds { get; set; } = 60;
}
