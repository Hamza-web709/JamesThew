using System.ComponentModel.DataAnnotations;
using JamesThew.Models;

namespace JamesThew.ViewModels;

public class EmailOtpViewModel
{
    public int ChallengeId { get; set; }
    public EmailOtpPurpose Purpose { get; set; }
    public string MaskedEmail { get; set; } = string.Empty;
    public string? ReturnUrl { get; set; }
    public int ResendCooldownSeconds { get; set; } = 60;

    [Required(ErrorMessage = "Enter the six-digit verification code.")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "Email verification code must be exactly six digits.")]
    public string Code { get; set; } = string.Empty;

    public string Title => Purpose == EmailOtpPurpose.Registration
        ? "Verify Your Email"
        : "Complete Sign In";
}
