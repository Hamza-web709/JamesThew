using JamesThew.Models;

namespace JamesThew.Services;

public interface IEmailOtpService
{
    Task<EmailOtpStartResult> SendRegistrationOtpAsync(ApplicationUser user);
    Task<EmailOtpStartResult> SendLoginOtpAsync(ApplicationUser user, string? returnUrl, bool rememberMe);
    Task<EmailOtpStartResult> SendPasswordResetOtpAsync(ApplicationUser user);
    Task<EmailOtpVerifyResult> VerifyAsync(int challengeId, EmailOtpPurpose purpose, string code);
    Task<EmailOtpVerifyResult> VerifyPasswordResetOtpAsync(string email, string code);
    Task<EmailOtpStartResult> ResendAsync(int challengeId, EmailOtpPurpose purpose);
    Task<EmailOtpStartResult> ResendPasswordResetOtpAsync(string email);
    Task InvalidateUserChallengesAsync(string userId);
}

public sealed record EmailOtpStartResult(bool Success, string Message, int? ChallengeId = null, string? MaskedEmail = null);
public sealed record EmailOtpVerifyResult(bool Success, string Message, string? UserId = null, string? ReturnUrl = null, bool RememberMe = false);
