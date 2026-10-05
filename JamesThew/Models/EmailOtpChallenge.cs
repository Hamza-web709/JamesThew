using System.ComponentModel.DataAnnotations;

namespace JamesThew.Models;

public class EmailOtpChallenge
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    [Required, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    public EmailOtpPurpose Purpose { get; set; }

    [Required, MaxLength(128)]
    public string CodeHash { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string CodeSalt { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? ReturnUrl { get; set; }

    public bool RememberMe { get; set; }
    public int FailedAttempts { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastSentAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
}

public enum EmailOtpPurpose
{
    Registration = 1,
    Login = 2
}
