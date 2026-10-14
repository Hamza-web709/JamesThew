using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using JamesThew.Authorization;
using JamesThew.Data;
using JamesThew.Models;
using JamesThew.Services;
using JamesThew.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace JamesThew.Tests;

[Collection("Foundation")]
public class PasswordResetTests(FoundationFixture fixture)
{
    private static string NewEmail() => "reset_" + Guid.NewGuid().ToString("N") + "@jamesthew.test";
    private static string NewPassword() => "Pass@" + Convert.ToHexString(RandomNumberGenerator.GetBytes(10)) + "!9A";

    private async Task<ApplicationUser> CreateTestUserAsync(string email, string password)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = "Reset Test User",
            EmailConfirmed = true
        };
        var result = await userMgr.CreateAsync(user, password);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(e => e.Description)));
        await userMgr.AddToRoleAsync(user, AppRoles.Member);
        return user;
    }

    [Fact]
    public async Task Req01_Forgot_password_page_loads_with_required_elements()
    {
        using var client = fixture.NewClient();
        var response = await client.GetAsync("/account/forgot-password");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Reset Your", html);
        Assert.Contains("Email Address", html);
        Assert.Contains("Send Verification Code", html);
        Assert.Contains("Return to Sign In", html);
    }

    [Fact]
    public async Task Req02_Login_page_contains_forgot_password_link()
    {
        using var client = fixture.NewClient();
        var response = await client.GetAsync("/account/login");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Forgot password?", html);
        Assert.Contains("href=\"/account/forgot-password\"", html);
    }

    [Fact]
    public async Task Req03_To_Req06_Neutral_enumeration_protection_for_existing_and_unknown_accounts()
    {
        using var client = fixture.NewClient();
        var existingEmail = NewEmail();
        var password = NewPassword();
        await CreateTestUserAsync(existingEmail, password);

        var unknownEmail = NewEmail();

        // 1. Existing account request
        var tokenExisting = await TestAuth.Token(client, "/account/forgot-password");
        var postExisting = await client.PostAsync("/account/forgot-password", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = tokenExisting,
            ["Email"] = existingEmail
        }));

        Assert.Equal(HttpStatusCode.Redirect, postExisting.StatusCode);
        Assert.Contains("/account/forgot-password/verify", postExisting.Headers.Location!.OriginalString);
        Assert.Contains(existingEmail, WebUtility.UrlDecode(postExisting.Headers.Location!.OriginalString));

        // Follow redirect to verify page and check neutral message
        var verifyExistingPage = await client.GetAsync(postExisting.Headers.Location!.OriginalString);
        var existingHtml = await verifyExistingPage.Content.ReadAsStringAsync();
        Assert.Contains("If an account exists for this email, a verification code has been sent.", existingHtml);

        // Req 05: Existing account receives PasswordReset challenge in database
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var challenge = await db.EmailOtpChallenges
                .Where(x => x.Email == existingEmail && x.Purpose == EmailOtpPurpose.PasswordReset)
                .OrderByDescending(x => x.CreatedAtUtc)
                .FirstOrDefaultAsync();

            Assert.NotNull(challenge);
            Assert.Equal(EmailOtpPurpose.PasswordReset, challenge.Purpose);
            Assert.Null(challenge.ConsumedAtUtc);
            Assert.Equal(64, challenge.CodeHash.Length);
        }

        // 2. Unknown account request
        var tokenUnknown = await TestAuth.Token(client, "/account/forgot-password");
        var postUnknown = await client.PostAsync("/account/forgot-password", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = tokenUnknown,
            ["Email"] = unknownEmail
        }));

        // Req 04: Unknown account request returns identical neutral response
        Assert.Equal(HttpStatusCode.Redirect, postUnknown.StatusCode);
        Assert.Contains("/account/forgot-password/verify", postUnknown.Headers.Location!.OriginalString);
        Assert.Contains(unknownEmail, WebUtility.UrlDecode(postUnknown.Headers.Location!.OriginalString));

        var verifyUnknownPage = await client.GetAsync(postUnknown.Headers.Location!.OriginalString);
        var unknownHtml = await verifyUnknownPage.Content.ReadAsStringAsync();
        Assert.Contains("If an account exists for this email, a verification code has been sent.", unknownHtml);

        // Req 06: Unknown account receives NO usable reset challenge in database
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var challenge = await db.EmailOtpChallenges
                .FirstOrDefaultAsync(x => x.Email == unknownEmail && x.Purpose == EmailOtpPurpose.PasswordReset);

            Assert.Null(challenge);
        }
    }

    [Fact]
    public async Task Req07_Req08_Password_reset_otp_is_six_digits_and_raw_otp_is_not_stored()
    {
        using var client = fixture.NewClient();
        var email = NewEmail();
        await CreateTestUserAsync(email, NewPassword());

        var token = await TestAuth.Token(client, "/account/forgot-password");
        await client.PostAsync("/account/forgot-password", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Email"] = email
        }));

        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var challenge = await db.EmailOtpChallenges.SingleAsync(x => x.Email == email && x.Purpose == EmailOtpPurpose.PasswordReset);

        var rawCode = TestAuth.LatestOtpFor(email, challenge.Id);
        Assert.Equal(6, rawCode.Length);
        Assert.True(rawCode.All(char.IsDigit));
        Assert.True(EmailOtpService.IsSixDigitCode(rawCode));

        // Req 08: Raw OTP is not stored
        Assert.DoesNotContain(rawCode, challenge.CodeHash);
        Assert.DoesNotContain(rawCode, challenge.CodeSalt);
    }

    [Fact]
    public async Task Req09_To_Req11_Wrong_otp_rejected_attempts_counted_and_max_5_enforced()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var emailOtp = scope.ServiceProvider.GetRequiredService<IEmailOtpService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var email = NewEmail();
        var user = new ApplicationUser { UserName = email, Email = email, DisplayName = "Attempts User", EmailConfirmed = true };
        await userMgr.CreateAsync(user, NewPassword());

        var start = await emailOtp.SendPasswordResetOtpAsync(user);
        Assert.True(start.Success);

        // Req 09 & 10: Wrong OTP rejected and failed attempt count increments
        for (var i = 1; i <= 5; i++)
        {
            var verifyWrong = await emailOtp.VerifyPasswordResetOtpAsync(email, "999999");
            Assert.False(verifyWrong.Success);
            Assert.Equal("The verification code is incorrect.", verifyWrong.Message);

            var inDb = await db.EmailOtpChallenges.SingleAsync(x => x.Id == start.ChallengeId);
            Assert.Equal(i, inDb.FailedAttempts);
        }

        // Req 11: 5-attempt maximum enforced
        var sixthAttempt = await emailOtp.VerifyPasswordResetOtpAsync(email, "999999");
        Assert.False(sixthAttempt.Success);
        Assert.Contains("Too many incorrect attempts", sixthAttempt.Message);

        // Even correct code should now be rejected due to attempt limit
        var correctCode = TestAuth.LatestOtpFor(email, start.ChallengeId);
        var correctAfterMax = await emailOtp.VerifyPasswordResetOtpAsync(email, correctCode);
        Assert.False(correctAfterMax.Success);
        Assert.Contains("Too many incorrect attempts", correctAfterMax.Message);
    }

    [Fact]
    public async Task Req12_Expired_reset_otp_is_rejected()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var emailOtp = scope.ServiceProvider.GetRequiredService<IEmailOtpService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var email = NewEmail();
        var user = new ApplicationUser { UserName = email, Email = email, DisplayName = "Expiry User", EmailConfirmed = true };
        await userMgr.CreateAsync(user, NewPassword());

        var start = await emailOtp.SendPasswordResetOtpAsync(user);
        var challenge = await db.EmailOtpChallenges.SingleAsync(x => x.Id == start.ChallengeId);
        challenge.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        var code = TestAuth.LatestOtpFor(email, start.ChallengeId);
        var result = await emailOtp.VerifyPasswordResetOtpAsync(email, code);
        Assert.False(result.Success);
        Assert.Contains("expired", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Req13_Resend_cooldown_of_60_seconds_enforced()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var emailOtp = scope.ServiceProvider.GetRequiredService<IEmailOtpService>();

        var email = NewEmail();
        var user = new ApplicationUser { UserName = email, Email = email, DisplayName = "Cooldown User", EmailConfirmed = true };
        await userMgr.CreateAsync(user, NewPassword());

        var start = await emailOtp.SendPasswordResetOtpAsync(user);
        Assert.True(start.Success);

        // Immediate resend must fail with cooldown notice
        var resendTooSoon = await emailOtp.ResendPasswordResetOtpAsync(email);
        Assert.False(resendTooSoon.Success);
        Assert.Contains("wait before requesting another code", resendTooSoon.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Req14_New_reset_otp_invalidates_old_reset_otp()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var emailOtp = scope.ServiceProvider.GetRequiredService<IEmailOtpService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var email = NewEmail();
        var user = new ApplicationUser { UserName = email, Email = email, DisplayName = "Invalidation User", EmailConfirmed = true };
        await userMgr.CreateAsync(user, NewPassword());

        var first = await emailOtp.SendPasswordResetOtpAsync(user);
        var firstCode = TestAuth.LatestOtpFor(email, first.ChallengeId);

        // Simulate 61 seconds later
        var firstChallenge = await db.EmailOtpChallenges.SingleAsync(x => x.Id == first.ChallengeId);
        firstChallenge.LastSentAtUtc = DateTime.UtcNow.AddSeconds(-65);
        await db.SaveChangesAsync();

        var second = await emailOtp.ResendPasswordResetOtpAsync(email);
        Assert.True(second.Success);
        var secondCode = TestAuth.LatestOtpFor(email, second.ChallengeId);

        // The first challenge is now marked consumed/invalidated
        await db.Entry(firstChallenge).ReloadAsync();
        Assert.NotNull(firstChallenge.ConsumedAtUtc);

        // First code fails
        var firstVerify = await emailOtp.VerifyPasswordResetOtpAsync(email, firstCode);
        Assert.False(firstVerify.Success);

        // Second code succeeds
        var secondVerify = await emailOtp.VerifyPasswordResetOtpAsync(email, secondCode);
        Assert.True(secondVerify.Success);
    }

    [Fact]
    public async Task Req17_Req18_Purpose_isolation_between_PasswordReset_and_Login()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var emailOtp = scope.ServiceProvider.GetRequiredService<IEmailOtpService>();

        var email = NewEmail();
        var user = new ApplicationUser { UserName = email, Email = email, DisplayName = "Isolation User", EmailConfirmed = true };
        await userMgr.CreateAsync(user, NewPassword());

        // Create reset OTP
        var resetStart = await emailOtp.SendPasswordResetOtpAsync(user);
        var resetCode = TestAuth.LatestOtpFor(email, resetStart.ChallengeId);

        // Req 17: Reset OTP cannot verify Login purpose
        var loginAttemptWithReset = await emailOtp.VerifyAsync(resetStart.ChallengeId!.Value, EmailOtpPurpose.Login, resetCode);
        Assert.False(loginAttemptWithReset.Success);
        Assert.Contains("not found", loginAttemptWithReset.Message, StringComparison.OrdinalIgnoreCase);

        // Create login OTP
        var loginStart = await emailOtp.SendLoginOtpAsync(user, "/account/status", false);
        var loginCode = TestAuth.LatestOtpFor(email, loginStart.ChallengeId);

        // Req 18: Login OTP cannot verify PasswordReset purpose
        var resetAttemptWithLogin = await emailOtp.VerifyAsync(loginStart.ChallengeId!.Value, EmailOtpPurpose.PasswordReset, loginCode);
        Assert.False(resetAttemptWithLogin.Success);
        Assert.Contains("not found", resetAttemptWithLogin.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Req15_Req16_Req19_To_Req27_Complete_end_to_end_password_reset_flow()
    {
        using var client = fixture.NewClient();
        var email = NewEmail();
        var oldPassword = NewPassword();
        var newPassword = "NewSecretPassword@1234!";

        await CreateTestUserAsync(email, oldPassword);

        // Step 1: Request password reset
        var forgotToken = await TestAuth.Token(client, "/account/forgot-password");
        var forgotPost = await client.PostAsync("/account/forgot-password", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = forgotToken,
            ["Email"] = email
        }));
        Assert.Equal(HttpStatusCode.Redirect, forgotPost.StatusCode);
        var verifyUrl = forgotPost.Headers.Location!.OriginalString;

        // Step 2: Get OTP from test email sink
        var code = TestAuth.LatestOtpFor(email);

        // Step 3: Verify OTP
        var verifyToken = await TestAuth.Token(client, verifyUrl);
        var verifyPost = await client.PostAsync("/account/forgot-password/verify", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = verifyToken,
            ["Email"] = email,
            ["Code"] = code
        }));

        // Req 15: Correct reset OTP verifies
        Assert.Equal(HttpStatusCode.Redirect, verifyPost.StatusCode);
        Assert.Contains("/account/reset-password", verifyPost.Headers.Location!.OriginalString);
        Assert.Contains("token=", verifyPost.Headers.Location!.OriginalString);

        // Req 16: Reset OTP does NOT authenticate user
        var statusCheck = await client.GetAsync("/account/status");
        Assert.Equal(HttpStatusCode.Redirect, statusCheck.StatusCode);
        Assert.Contains("/account/login", statusCheck.Headers.Location!.OriginalString);

        // Req 25: Consumed reset OTP cannot be reused
        var reuseVerify = await client.PostAsync("/account/forgot-password/verify", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await TestAuth.Token(client, verifyUrl),
            ["Email"] = email,
            ["Code"] = code
        }));
        var reuseHtml = await reuseVerify.Content.ReadAsStringAsync();
        Assert.Contains("The verification code is incorrect.", reuseHtml);

        var resetPageUrl = verifyPost.Headers.Location!.OriginalString;
        var resetPageHtml = await client.GetStringAsync(resetPageUrl);
        var tokenMatch = Regex.Match(resetPageHtml, "name=\"Token\"[^>]*value=\"([^\"]+)\"");
        Assert.True(tokenMatch.Success, "Expected Token hidden input in reset password form.");
        var resetAuthorizationToken = WebUtility.HtmlDecode(tokenMatch.Groups[1].Value);

        // Req 19: New password and confirm password mismatch rejected
        var mismatchCsrf = await TestAuth.Token(client, resetPageUrl);
        var mismatchPost = await client.PostAsync("/account/reset-password", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = mismatchCsrf,
            ["Token"] = resetAuthorizationToken,
            ["NewPassword"] = newPassword,
            ["ConfirmPassword"] = "DifferentPassword123!"
        }));
        var mismatchHtml = await mismatchPost.Content.ReadAsStringAsync();
        Assert.Contains("The password and confirmation password do not match.", mismatchHtml);

        // Req 20: Identity password policy enforced (too short)
        var weakCsrf = await TestAuth.Token(client, resetPageUrl);
        var weakPost = await client.PostAsync("/account/reset-password", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = weakCsrf,
            ["Token"] = resetAuthorizationToken,
            ["NewPassword"] = "Short1!",
            ["ConfirmPassword"] = "Short1!"
        }));
        var weakHtml = await weakPost.Content.ReadAsStringAsync();
        Assert.Contains("Password must be at least 12 characters", weakHtml);

        // Req 27: Account target cannot be changed by tampering with token
        var tamperedCsrf = await TestAuth.Token(client, resetPageUrl);
        var tamperedPost = await client.PostAsync("/account/reset-password", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = tamperedCsrf,
            ["Token"] = resetAuthorizationToken + "tampered",
            ["NewPassword"] = newPassword,
            ["ConfirmPassword"] = newPassword
        }));
        var tamperedHtml = await tamperedPost.Content.ReadAsStringAsync();
        Assert.Contains("expired or is invalid", tamperedHtml);

        // Req 21: Password reset succeeds through UserManager
        var validCsrf = await TestAuth.Token(client, resetPageUrl);
        var validPost = await client.PostAsync("/account/reset-password", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = validCsrf,
            ["Token"] = resetAuthorizationToken,
            ["NewPassword"] = newPassword,
            ["ConfirmPassword"] = newPassword
        }));

        Assert.Equal(HttpStatusCode.Redirect, validPost.StatusCode);
        Assert.Equal("/account/reset-password/confirmation", validPost.Headers.Location!.OriginalString);

        // Confirmation page displays success message and Sign In button
        var confirmationPage = await client.GetAsync("/account/reset-password/confirmation");
        var confirmationHtml = await confirmationPage.Content.ReadAsStringAsync();
        Assert.Contains("Password successfully changed.", confirmationHtml);
        Assert.Contains("Sign In", confirmationHtml);

        // Req 26: Reset authorization cannot be replayed
        var replayCsrf = await TestAuth.Token(client, resetPageUrl);
        var replayPost = await client.PostAsync("/account/reset-password", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = replayCsrf,
            ["Token"] = resetAuthorizationToken,
            ["NewPassword"] = "AnotherPassword@9876!",
            ["ConfirmPassword"] = "AnotherPassword@9876!"
        }));
        var replayHtml = await replayPost.Content.ReadAsStringAsync();
        Assert.Contains("Invalid token", replayHtml);

        // Req 22: Old password no longer works
        var oldLogin = await client.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await TestAuth.Token(client, "/account/login"),
            ["Email"] = email,
            ["Password"] = oldPassword
        }));
        var oldLoginHtml = await oldLogin.Content.ReadAsStringAsync();
        Assert.Contains("Unable to sign in", oldLoginHtml);

        // Req 23 & 24: New password works and NEXT login still requires Login Email OTP
        var newLogin = await client.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await TestAuth.Token(client, "/account/login"),
            ["Email"] = email,
            ["Password"] = newPassword
        }));
        Assert.Equal(HttpStatusCode.Redirect, newLogin.StatusCode);
        Assert.Contains("/account/otp/email", newLogin.Headers.Location!.OriginalString);

        // Complete Login Email OTP
        var loginOtpCode = TestAuth.LatestOtpFor(email);
        var completeLogin = await client.PostAsync("/account/otp/email", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await TestAuth.Token(client, newLogin.Headers.Location!.OriginalString),
            ["ChallengeId"] = TestAuth.ChallengeIdFromLocation(newLogin.Headers.Location!.OriginalString).ToString(),
            ["Purpose"] = EmailOtpPurpose.Login.ToString(),
            ["MaskedEmail"] = EmailOtpService.MaskEmail(email),
            ["Code"] = loginOtpCode
        }));
        Assert.Equal(HttpStatusCode.Redirect, completeLogin.StatusCode);
        Assert.Equal("/account/status", completeLogin.Headers.Location!.OriginalString);

        var statusAfterLogin = await client.GetAsync("/account/status");
        Assert.Equal(HttpStatusCode.OK, statusAfterLogin.StatusCode);
        var statusHtml = await statusAfterLogin.Content.ReadAsStringAsync();
        Assert.Contains("Standard Registered Member", statusHtml);
    }

    [Fact]
    public async Task Req28_Smtp_failure_does_not_disclose_account_existence_and_leaves_no_usable_challenge()
    {
        // When email delivery fails, the user experiences the same neutral response
        // and no usable challenge is left in the DB.
        var email = NewEmail();
        var user = await CreateTestUserAsync(email, NewPassword());

        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var failingSender = new FailingEmailSender();
        var service = new EmailOtpService(db, failingSender, NullLogger<EmailOtpService>.Instance);

        var result = await service.SendPasswordResetOtpAsync(user);
        Assert.False(result.Success);

        // Verify challenge was cleaned up / rolled back
        var challenges = await db.EmailOtpChallenges
            .Where(x => x.UserId == user.Id && x.Purpose == EmailOtpPurpose.PasswordReset)
            .ToListAsync();
        Assert.Empty(challenges);
    }

    [Fact]
    public void Req29_Req30_SmtpEmailSender_constructs_from_name_and_falls_back_safely()
    {
        // Req 29: FromName is included in real SMTP sender construction
        var configWithCustomName = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:Smtp:Host"] = "smtp.gmail.com",
                ["Email:Smtp:From"] = "test@jamesthew.com",
                ["Email:Smtp:FromName"] = "JamesThew Sanctuary"
            })
            .Build();

        var address1 = SmtpEmailSender.CreateSenderAddress(configWithCustomName);
        Assert.Equal("test@jamesthew.com", address1.Address);
        Assert.Equal("JamesThew Sanctuary", address1.DisplayName);

        // Custom name
        var configCustom = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:Smtp:Host"] = "smtp.gmail.com",
                ["Email:Smtp:From"] = "test@jamesthew.com",
                ["Email:Smtp:FromName"] = "Chef Master"
            })
            .Build();
        var addressCustom = SmtpEmailSender.CreateSenderAddress(configCustom);
        Assert.Equal("Chef Master", addressCustom.DisplayName);

        // Req 30: Missing FromName falls back to JamesThew Sanctuary
        var configMissingName = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:Smtp:Host"] = "smtp.gmail.com",
                ["Email:Smtp:From"] = "test@jamesthew.com"
            })
            .Build();

        var address2 = SmtpEmailSender.CreateSenderAddress(configMissingName);
        Assert.Equal("test@jamesthew.com", address2.Address);
        Assert.Equal("JamesThew Sanctuary", address2.DisplayName);

        // Blank FromName falls back to JamesThew Sanctuary
        var configBlankName = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:Smtp:Host"] = "smtp.gmail.com",
                ["Email:Smtp:From"] = "test@jamesthew.com",
                ["Email:Smtp:FromName"] = "   "
            })
            .Build();

        var address3 = SmtpEmailSender.CreateSenderAddress(configBlankName);
        Assert.Equal("JamesThew Sanctuary", address3.DisplayName);
    }

    [Fact]
    public async Task Email_branding_subjects_and_body_content_match_standards()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var emailOtp = scope.ServiceProvider.GetRequiredService<IEmailOtpService>();

        var regEmail = NewEmail();
        var regUser = new ApplicationUser { UserName = regEmail, Email = regEmail, DisplayName = "Branding User 1", EmailConfirmed = false };
        await userMgr.CreateAsync(regUser, NewPassword());
        await emailOtp.SendRegistrationOtpAsync(regUser);

        var loginEmail = NewEmail();
        var loginUser = new ApplicationUser { UserName = loginEmail, Email = loginEmail, DisplayName = "Branding User 2", EmailConfirmed = true };
        await userMgr.CreateAsync(loginUser, NewPassword());
        await emailOtp.SendLoginOtpAsync(loginUser, "/", false);

        var resetEmail = NewEmail();
        var resetUser = new ApplicationUser { UserName = resetEmail, Email = resetEmail, DisplayName = "Branding User 3", EmailConfirmed = true };
        await userMgr.CreateAsync(resetUser, NewPassword());
        await emailOtp.SendPasswordResetOtpAsync(resetUser);

        lock (DevelopmentEmailSender.SentMessages)
        {
            var regMsg = DevelopmentEmailSender.SentMessages.Last(x => x.ToEmail == regEmail);
            Assert.Equal("Your JamesThew verification code", regMsg.Subject);
            Assert.Contains("expires in 5 minutes", regMsg.HtmlBody);
            Assert.Contains("can be used once", regMsg.HtmlBody);
            Assert.Contains("Never share this code with anyone.", regMsg.HtmlBody);

            var loginMsg = DevelopmentEmailSender.SentMessages.Last(x => x.ToEmail == loginEmail);
            Assert.Equal("Your JamesThew sign-in code", loginMsg.Subject);
            Assert.Contains("expires in 5 minutes", loginMsg.HtmlBody);
            Assert.Contains("Never share this code with anyone.", loginMsg.HtmlBody);

            var resetMsg = DevelopmentEmailSender.SentMessages.Last(x => x.ToEmail == resetEmail);
            Assert.Equal("Your JamesThew password reset code", resetMsg.Subject);
            Assert.Contains("expires in 5 minutes", resetMsg.HtmlBody);
            Assert.Contains("Never share this code with anyone.", resetMsg.HtmlBody);
        }
    }

    private class FailingEmailSender : IEmailSender
    {
        public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
        {
            throw new EmailSendException("Simulated SMTP delivery failure");
        }
    }
}
