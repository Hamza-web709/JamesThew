using System.Security.Cryptography;
using System.Text;
using JamesThew.Data;
using JamesThew.Models;
using Microsoft.EntityFrameworkCore;

namespace JamesThew.Services;

public class EmailOtpService(ApplicationDbContext db, IEmailSender emailSender, ILogger<EmailOtpService> logger) : IEmailOtpService
{
    public const int CodeLength = 6;
    public static readonly TimeSpan Expiry = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);
    public const int MaxFailedAttempts = 5;

    public async Task<EmailOtpStartResult> SendRegistrationOtpAsync(ApplicationUser user) =>
        await CreateAndSendAsync(user, EmailOtpPurpose.Registration, null, false);

    public async Task<EmailOtpStartResult> SendLoginOtpAsync(ApplicationUser user, string? returnUrl, bool rememberMe) =>
        await CreateAndSendAsync(user, EmailOtpPurpose.Login, returnUrl, rememberMe);

    public async Task<EmailOtpStartResult> ResendAsync(int challengeId, EmailOtpPurpose purpose)
    {
        var challenge = await db.EmailOtpChallenges.Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == challengeId && x.Purpose == purpose);
        if (challenge is null || challenge.ConsumedAtUtc.HasValue)
            return new EmailOtpStartResult(false, "Verification session was not found. Please start again.");

        var now = DateTime.UtcNow;
        if (challenge.LastSentAtUtc.Add(ResendCooldown) > now)
            return new EmailOtpStartResult(false, "Please wait before requesting another code.", challenge.Id, MaskEmail(challenge.Email));

        var code = GenerateCode();
        var salt = GenerateSalt();
        challenge.CodeSalt = salt;
        challenge.CodeHash = HashCode(code, salt);
        challenge.ExpiresAtUtc = now.Add(Expiry);
        challenge.LastSentAtUtc = now;
        challenge.FailedAttempts = 0;
        await db.SaveChangesAsync();

        try
        {
            await SendEmailAsync(challenge.Email, purpose, code, challenge.Id);
        }
        catch (EmailSendException exception)
        {
            logger.LogWarning(exception, "Unable to resend email OTP for challenge {ChallengeId}.", challenge.Id);
            return new EmailOtpStartResult(false, exception.Message, challenge.Id, MaskEmail(challenge.Email));
        }

        return new EmailOtpStartResult(true, "A new verification code has been sent.", challenge.Id, MaskEmail(challenge.Email));
    }

    public async Task<EmailOtpVerifyResult> VerifyAsync(int challengeId, EmailOtpPurpose purpose, string code)
    {
        var challenge = await db.EmailOtpChallenges.FirstOrDefaultAsync(x => x.Id == challengeId && x.Purpose == purpose);
        if (challenge is null || challenge.ConsumedAtUtc.HasValue)
            return new EmailOtpVerifyResult(false, "Verification session was not found. Please start again.");

        if (DateTime.UtcNow > challenge.ExpiresAtUtc)
            return new EmailOtpVerifyResult(false, "The verification code has expired. Request a new code.");

        if (challenge.FailedAttempts >= MaxFailedAttempts)
            return new EmailOtpVerifyResult(false, "Too many incorrect attempts. Request a new code.");

        if (!IsSixDigitCode(code) || !Matches(code, challenge.CodeSalt, challenge.CodeHash))
        {
            challenge.FailedAttempts++;
            await db.SaveChangesAsync();
            return new EmailOtpVerifyResult(false, "The verification code is incorrect.");
        }

        challenge.ConsumedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return new EmailOtpVerifyResult(true, "Verification succeeded.", challenge.UserId, challenge.ReturnUrl, challenge.RememberMe);
    }

    private async Task<EmailOtpStartResult> CreateAndSendAsync(ApplicationUser user, EmailOtpPurpose purpose, string? returnUrl, bool rememberMe)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
            return new EmailOtpStartResult(false, "This account does not have a deliverable email address.");

        var now = DateTime.UtcNow;
        var code = GenerateCode();
        var salt = GenerateSalt();

        var previousChallenges = await db.EmailOtpChallenges
            .Where(x => x.UserId == user.Id && x.Purpose == purpose && x.ConsumedAtUtc == null)
            .ToListAsync();
        foreach (var previous in previousChallenges)
        {
            previous.ConsumedAtUtc = now;
        }

        var challenge = new EmailOtpChallenge
        {
            UserId = user.Id,
            Email = user.Email,
            Purpose = purpose,
            CodeSalt = salt,
            CodeHash = HashCode(code, salt),
            ReturnUrl = returnUrl,
            RememberMe = rememberMe,
            CreatedAtUtc = now,
            LastSentAtUtc = now,
            ExpiresAtUtc = now.Add(Expiry)
        };
        db.EmailOtpChallenges.Add(challenge);
        await db.SaveChangesAsync();

        try
        {
            await SendEmailAsync(user.Email, purpose, code, challenge.Id);
        }
        catch (EmailSendException exception)
        {
            logger.LogWarning(exception, "Unable to send email OTP for challenge {ChallengeId}.", challenge.Id);
            return new EmailOtpStartResult(false, exception.Message, challenge.Id, MaskEmail(user.Email));
        }

        return new EmailOtpStartResult(true, "A verification code has been sent.", challenge.Id, MaskEmail(user.Email));
    }

    private async Task SendEmailAsync(string email, EmailOtpPurpose purpose, string code, int challengeId)
    {
        var subject = purpose == EmailOtpPurpose.Registration
            ? "Verify your JamesThew account"
            : "Your JamesThew sign-in code";

        var intro = purpose == EmailOtpPurpose.Registration
            ? "Use this six-digit code to verify your new JamesThew member account."
            : "Use this six-digit code to finish signing in to JamesThew.";

        var body = $"""
            <div style="font-family:Arial,sans-serif;line-height:1.6;color:#181b20">
              <!-- challenge:{challengeId} -->
              <h2 style="font-family:Georgia,serif">JamesThew Verification Code</h2>
              <p>{intro}</p>
              <p style="font-size:28px;letter-spacing:8px;font-weight:700">{code}</p>
              <p>This code expires in 5 minutes and can be used once.</p>
            </div>
            """;

        await emailSender.SendAsync(email, subject, body);
    }

    private static string GenerateCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
    private static string GenerateSalt() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

    private static string HashCode(string code, string salt)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{salt}:{code}"));
        return Convert.ToHexString(bytes);
    }

    private static bool Matches(string code, string salt, string expectedHash)
    {
        var actual = Convert.FromHexString(HashCode(code, salt));
        var expected = Convert.FromHexString(expectedHash);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    public static bool IsSixDigitCode(string? code) => code is { Length: CodeLength } && code.All(char.IsDigit);

    public static string MaskEmail(string email)
    {
        var parts = email.Split('@', 2);
        if (parts.Length != 2)
            return "your email";
        var local = parts[0];
        var visible = local.Length <= 2 ? local[..1] : local[..Math.Min(2, local.Length)];
        return $"{visible}***@{parts[1]}";
    }
}
