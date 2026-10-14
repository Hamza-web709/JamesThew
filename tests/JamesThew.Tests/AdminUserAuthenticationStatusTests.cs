using System.Net;
using System.Security.Cryptography;
using JamesThew.Authorization;
using JamesThew.Data;
using JamesThew.Models;
using JamesThew.Services;
using JamesThew.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace JamesThew.Tests;

[Collection("Foundation")]
public class AdminUserAuthenticationStatusTests(FoundationFixture fixture)
{
    private static string NewEmail() => "auth_u5b_" + Guid.NewGuid().ToString("N") + "@jamesthew.test";
    private static string NewPassword() => "Pass@" + Convert.ToHexString(RandomNumberGenerator.GetBytes(10)) + "!9A";

    private async Task<ApplicationUser> CreateTestUserAsync(
        string email,
        string password,
        bool isActive = true,
        bool emailConfirmed = true,
        string role = AppRoles.Member,
        bool isDemoAdminBypass = false)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = "U5B Test User",
            EmailConfirmed = emailConfirmed,
            IsActive = isActive,
            IsDemoAdminOtpBypass = isDemoAdminBypass
        };
        var result = await userMgr.CreateAsync(user, password);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(e => e.Description)));
        await userMgr.AddToRoleAsync(user, role);
        return user;
    }

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        var adminEmail = "admin_" + Guid.NewGuid().ToString("N") + "@jamesthew.test";
        var adminPassword = NewPassword();
        await CreateTestUserAsync(adminEmail, adminPassword, isActive: true, emailConfirmed: true, role: AppRoles.Admin);
        var client = fixture.NewClient();
        await TestAuth.Login(client, adminEmail, adminPassword);
        return client;
    }

    private async Task<string> ObtainResetTokenWhileActiveAsync(HttpClient client, string email)
    {
        var forgotResponse = await client.PostAsync("/account/forgot-password", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await TestAuth.Token(client, "/account/forgot-password"),
            ["Email"] = email
        }));
        Assert.Equal(HttpStatusCode.Redirect, forgotResponse.StatusCode);

        int challengeId;
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var challenge = await db.EmailOtpChallenges
                .Where(x => x.Email == email && x.Purpose == EmailOtpPurpose.PasswordReset)
                .OrderByDescending(x => x.CreatedAtUtc)
                .FirstAsync();
            challengeId = challenge.Id;
        }

        var code = TestAuth.LatestOtpFor(email, challengeId);
        var verifyUrl = "/account/forgot-password/verify?email=" + WebUtility.UrlEncode(email);
        var verifyResponse = await client.PostAsync("/account/forgot-password/verify", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await TestAuth.Token(client, verifyUrl),
            ["Email"] = email,
            ["Code"] = code
        }));
        Assert.Equal(HttpStatusCode.Redirect, verifyResponse.StatusCode);
        var location = verifyResponse.Headers.Location!.OriginalString;
        var tokenIdx = location.IndexOf("token=", StringComparison.Ordinal);
        Assert.True(tokenIdx >= 0);
        var token = location[(tokenIdx + 6)..];
        Assert.False(string.IsNullOrWhiteSpace(token));
        return WebUtility.UrlDecode(token);
    }

    [Fact]
    public async Task Req01_Active_user_can_login_normally()
    {
        using var client = fixture.NewClient();
        var email = NewEmail();
        var password = NewPassword();
        await CreateTestUserAsync(email, password, isActive: true, emailConfirmed: true);

        var loginResponse = await TestAuth.Login(client, email, password);
        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);

        var profileResponse = await client.GetAsync("/account/status");
        Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode);
        var html = await profileResponse.Content.ReadAsStringAsync();
        Assert.Contains(email, html);
    }

    [Fact]
    public async Task Req02_Inactive_user_with_correct_password_cannot_login()
    {
        using var client = fixture.NewClient();
        var email = NewEmail();
        var password = NewPassword();
        await CreateTestUserAsync(email, password, isActive: false, emailConfirmed: true);

        var response = await client.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await TestAuth.Token(client, "/account/login"),
            ["Email"] = email,
            ["Password"] = password,
            ["ReturnUrl"] = "/"
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("This account is currently inactive. Please contact an administrator.", html);
    }

    [Fact]
    public async Task Req03_Inactive_user_receives_no_Login_OTP()
    {
        TestAuth.ClearMessages();
        using var client = fixture.NewClient();
        var email = NewEmail();
        var password = NewPassword();
        var user = await CreateTestUserAsync(email, password, isActive: false, emailConfirmed: true);

        await client.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await TestAuth.Token(client, "/account/login"),
            ["Email"] = email,
            ["Password"] = password,
            ["ReturnUrl"] = "/"
        }));

        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var loginChallengesCount = await db.EmailOtpChallenges
            .CountAsync(c => c.UserId == user.Id && c.Purpose == EmailOtpPurpose.Login);
        Assert.Equal(0, loginChallengesCount);

        lock (DevelopmentEmailSender.SentMessages)
        {
            var sentToUser = DevelopmentEmailSender.SentMessages.Count(m => m.ToEmail.Equals(email, StringComparison.OrdinalIgnoreCase));
            Assert.Equal(0, sentToUser);
        }
    }

    [Fact]
    public async Task Req04_Inactive_user_cannot_reach_authenticated_session()
    {
        using var client = fixture.NewClient();
        var email = NewEmail();
        var password = NewPassword();
        await CreateTestUserAsync(email, password, isActive: false, emailConfirmed: true);

        await client.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await TestAuth.Token(client, "/account/login"),
            ["Email"] = email,
            ["Password"] = password
        }));

        var statusResponse = await client.GetAsync("/account/status");
        Assert.Equal(HttpStatusCode.Redirect, statusResponse.StatusCode);
        Assert.Contains("/account/login", statusResponse.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Req05_Wrong_password_behavior_remains_unchanged_for_inactive_user()
    {
        using var client = fixture.NewClient();
        var email = NewEmail();
        var password = NewPassword();
        await CreateTestUserAsync(email, password, isActive: false, emailConfirmed: true);

        var response = await client.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await TestAuth.Token(client, "/account/login"),
            ["Email"] = email,
            ["Password"] = "WrongPassword999!"
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Unable to sign in. Check your details or try again later.", html);
        Assert.DoesNotContain("inactive", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Req06_Pending_Login_OTP_becomes_unusable_if_user_turns_inactive()
    {
        using var client = fixture.NewClient();
        var email = NewEmail();
        var password = NewPassword();
        var user = await CreateTestUserAsync(email, password, isActive: true, emailConfirmed: true);

        var loginPost = await client.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await TestAuth.Token(client, "/account/login"),
            ["Email"] = email,
            ["Password"] = password,
            ["ReturnUrl"] = "/"
        }));
        Assert.Equal(HttpStatusCode.Redirect, loginPost.StatusCode);
        var otpLocation = loginPost.Headers.Location!.OriginalString;
        var challengeId = TestAuth.ChallengeIdFromLocation(otpLocation);

        // Deactivate user in database
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var dbUser = await db.Users.FindAsync(user.Id);
            Assert.NotNull(dbUser);
            dbUser.IsActive = false;
            await db.SaveChangesAsync();
        }

        // Visiting OTP page now redirects to login with error
        var otpGet = await client.GetAsync(otpLocation);
        Assert.Equal(HttpStatusCode.Redirect, otpGet.StatusCode);
        Assert.Contains("/account/login", otpGet.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Req07_Correct_pending_OTP_cannot_sign_in_inactive_user()
    {
        using var client = fixture.NewClient();
        var email = NewEmail();
        var password = NewPassword();
        var user = await CreateTestUserAsync(email, password, isActive: true, emailConfirmed: true);

        var loginPost = await client.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await TestAuth.Token(client, "/account/login"),
            ["Email"] = email,
            ["Password"] = password,
            ["ReturnUrl"] = "/"
        }));
        Assert.Equal(HttpStatusCode.Redirect, loginPost.StatusCode);
        var otpLocation = loginPost.Headers.Location!.OriginalString;
        var challengeId = TestAuth.ChallengeIdFromLocation(otpLocation);
        var code = TestAuth.LatestOtpFor(email, challengeId);

        // Deactivate user while challenge is pending
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var dbUser = await db.Users.FindAsync(user.Id);
            Assert.NotNull(dbUser);
            dbUser.IsActive = false;
            await db.SaveChangesAsync();
        }

        // Submit the correct OTP code (using login page token)
        var otpPost = await client.PostAsync("/account/otp/email", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await TestAuth.Token(client, "/account/login"),
            ["ChallengeId"] = challengeId.ToString(),
            ["Purpose"] = EmailOtpPurpose.Login.ToString(),
            ["MaskedEmail"] = EmailOtpService.MaskEmail(email),
            ["Code"] = code
        }));

        Assert.Equal(HttpStatusCode.Redirect, otpPost.StatusCode);
        Assert.Contains("/account/login", otpPost.Headers.Location?.OriginalString);

        // Verify challenge was consumed and cannot be reused
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var challenge = await db.EmailOtpChallenges.FindAsync(challengeId);
            Assert.NotNull(challenge);
            Assert.NotNull(challenge.ConsumedAtUtc);
        }

        // Verify no session was created
        var profileResponse = await client.GetAsync("/account/status");
        Assert.Equal(HttpStatusCode.Redirect, profileResponse.StatusCode);
        Assert.Contains("/account/login", profileResponse.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Req08_Demo_admin_OTP_bypass_does_not_bypass_IsActive()
    {
        using var client = fixture.NewClient();
        var email = NewEmail();
        var password = NewPassword();
        await CreateTestUserAsync(email, password, isActive: false, emailConfirmed: true, role: AppRoles.Admin, isDemoAdminBypass: true);

        var response = await client.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await TestAuth.Token(client, "/account/login"),
            ["Email"] = email,
            ["Password"] = password
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("This account is currently inactive. Please contact an administrator.", html);

        var adminResponse = await client.GetAsync("/admin/users");
        Assert.Equal(HttpStatusCode.Redirect, adminResponse.StatusCode);
        Assert.Contains("/account/login", adminResponse.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Req09_Inactive_confirmed_email_user_still_cannot_login()
    {
        using var client = fixture.NewClient();
        var email = NewEmail();
        var password = NewPassword();
        await CreateTestUserAsync(email, password, isActive: false, emailConfirmed: true);

        var response = await client.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await TestAuth.Token(client, "/account/login"),
            ["Email"] = email,
            ["Password"] = password
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("This account is currently inactive. Please contact an administrator.", html);
    }

    [Fact]
    public async Task Req10_Identity_temporary_lockout_remains_separate_from_IsActive()
    {
        using var client = fixture.NewClient();
        var email = NewEmail();
        var password = NewPassword();
        var user = await CreateTestUserAsync(email, password, isActive: false, emailConfirmed: true);

        await client.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await TestAuth.Token(client, "/account/login"),
            ["Email"] = email,
            ["Password"] = password
        }));

        await using var scope = fixture.Services.CreateAsyncScope();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var reloaded = await userMgr.FindByIdAsync(user.Id);
        Assert.NotNull(reloaded);
        Assert.False(await userMgr.IsLockedOutAsync(reloaded));
        Assert.Equal(0, await userMgr.GetAccessFailedCountAsync(reloaded));
    }

    [Fact]
    public async Task Req11_Unknown_email_receives_neutral_response_on_forgot_password()
    {
        using var client = fixture.NewClient();
        var unknownEmail = "unknown_" + Guid.NewGuid().ToString("N") + "@test.com";

        var response = await client.PostAsync("/account/forgot-password", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await TestAuth.Token(client, "/account/forgot-password"),
            ["Email"] = unknownEmail
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/account/forgot-password/verify", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Req12_Inactive_email_receives_same_neutral_response_on_forgot_password()
    {
        using var client = fixture.NewClient();
        var email = NewEmail();
        var password = NewPassword();
        await CreateTestUserAsync(email, password, isActive: false, emailConfirmed: true);

        var response = await client.PostAsync("/account/forgot-password", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await TestAuth.Token(client, "/account/forgot-password"),
            ["Email"] = email
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/account/forgot-password/verify", response.Headers.Location?.OriginalString);

        var verifyPage = await client.GetAsync(response.Headers.Location!.OriginalString);
        var html = await verifyPage.Content.ReadAsStringAsync();
        Assert.DoesNotContain("inactive", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Enter the six-digit code", html);
    }

    [Fact]
    public async Task Req13_Inactive_email_generates_no_PasswordReset_OTP()
    {
        using var client = fixture.NewClient();
        var email = NewEmail();
        var password = NewPassword();
        var user = await CreateTestUserAsync(email, password, isActive: false, emailConfirmed: true);

        await client.PostAsync("/account/forgot-password", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await TestAuth.Token(client, "/account/forgot-password"),
            ["Email"] = email
        }));

        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var resetChallengesCount = await db.EmailOtpChallenges
            .CountAsync(c => c.UserId == user.Id && c.Purpose == EmailOtpPurpose.PasswordReset);
        Assert.Equal(0, resetChallengesCount);
    }

    [Fact]
    public async Task Req14_Inactive_email_triggers_no_reset_email_in_sink()
    {
        TestAuth.ClearMessages();
        using var client = fixture.NewClient();
        var email = NewEmail();
        var password = NewPassword();
        await CreateTestUserAsync(email, password, isActive: false, emailConfirmed: true);

        await client.PostAsync("/account/forgot-password", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await TestAuth.Token(client, "/account/forgot-password"),
            ["Email"] = email
        }));

        await client.PostAsync("/account/forgot-password/resend", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await TestAuth.Token(client, "/account/forgot-password/verify?email=" + WebUtility.UrlEncode(email)),
            ["Email"] = email
        }));

        lock (DevelopmentEmailSender.SentMessages)
        {
            var sentToUser = DevelopmentEmailSender.SentMessages.Count(m => m.ToEmail.Equals(email, StringComparison.OrdinalIgnoreCase));
            Assert.Equal(0, sentToUser);
        }
    }

    [Fact]
    public async Task Req15_Active_user_forgot_password_flow_still_works()
    {
        TestAuth.ClearMessages();
        using var client = fixture.NewClient();
        var email = NewEmail();
        var password = NewPassword();
        var user = await CreateTestUserAsync(email, password, isActive: true, emailConfirmed: true);

        var forgotResponse = await client.PostAsync("/account/forgot-password", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await TestAuth.Token(client, "/account/forgot-password"),
            ["Email"] = email
        }));
        Assert.Equal(HttpStatusCode.Redirect, forgotResponse.StatusCode);

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var challenge = await db.EmailOtpChallenges
                .Where(c => c.UserId == user.Id && c.Purpose == EmailOtpPurpose.PasswordReset)
                .OrderByDescending(c => c.CreatedAtUtc)
                .FirstOrDefaultAsync();
            Assert.NotNull(challenge);
        }

        lock (DevelopmentEmailSender.SentMessages)
        {
            var sent = DevelopmentEmailSender.SentMessages.Any(m => m.ToEmail.Equals(email, StringComparison.OrdinalIgnoreCase));
            Assert.True(sent);
        }
    }

    [Fact]
    public async Task Req16_Reset_OTP_issued_while_active_becomes_unusable_after_IsActive_false()
    {
        using var client = fixture.NewClient();
        var email = NewEmail();
        var password = NewPassword();
        var user = await CreateTestUserAsync(email, password, isActive: true, emailConfirmed: true);

        // Issue OTP while active
        await client.PostAsync("/account/forgot-password", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await TestAuth.Token(client, "/account/forgot-password"),
            ["Email"] = email
        }));

        int challengeId;
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var c = await db.EmailOtpChallenges.Where(x => x.UserId == user.Id && x.Purpose == EmailOtpPurpose.PasswordReset).SingleAsync();
            challengeId = c.Id;
        }

        var code = TestAuth.LatestOtpFor(email, challengeId);

        // Deactivate user in DB
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var dbUser = await db.Users.FindAsync(user.Id);
            Assert.NotNull(dbUser);
            dbUser.IsActive = false;
            await db.SaveChangesAsync();
        }

        // Try verifying OTP
        var verifyPost = await client.PostAsync("/account/forgot-password/verify", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await TestAuth.Token(client, "/account/forgot-password/verify?email=" + WebUtility.UrlEncode(email)),
            ["Email"] = email,
            ["Code"] = code
        }));

        Assert.Equal(HttpStatusCode.OK, verifyPost.StatusCode);
        var html = await verifyPost.Content.ReadAsStringAsync();
        Assert.Contains("The verification code is incorrect.", html);
    }

    [Fact]
    public async Task Req17_Inactive_account_cannot_receive_reset_authorization()
    {
        using var client = fixture.NewClient();
        var email = NewEmail();
        var password = NewPassword();
        var user = await CreateTestUserAsync(email, password, isActive: true, emailConfirmed: true);

        await client.PostAsync("/account/forgot-password", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await TestAuth.Token(client, "/account/forgot-password"),
            ["Email"] = email
        }));

        int challengeId;
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            challengeId = (await db.EmailOtpChallenges.Where(x => x.UserId == user.Id).SingleAsync()).Id;
        }
        var code = TestAuth.LatestOtpFor(email, challengeId);

        // Deactivate
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var dbUser = await db.Users.FindAsync(user.Id);
            Assert.NotNull(dbUser);
            dbUser.IsActive = false;
            await db.SaveChangesAsync();
        }

        var verifyPost = await client.PostAsync("/account/forgot-password/verify", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await TestAuth.Token(client, "/account/forgot-password/verify?email=" + WebUtility.UrlEncode(email)),
            ["Email"] = email,
            ["Code"] = code
        }));

        Assert.Equal(HttpStatusCode.OK, verifyPost.StatusCode);
        Assert.Null(verifyPost.Headers.Location);
    }

    [Fact]
    public async Task Req18_Final_reset_POST_rechecks_IsActive()
    {
        using var client = fixture.NewClient();
        var email = NewEmail();
        var password = NewPassword();
        var user = await CreateTestUserAsync(email, password, isActive: true, emailConfirmed: true);

        // Generate token through active path
        var protectedToken = await ObtainResetTokenWhileActiveAsync(client, email);

        // Verify GET loads while active
        var getResponse = await client.GetAsync("/account/reset-password?token=" + WebUtility.UrlEncode(protectedToken));
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        // Deactivate user in DB
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var dbUser = await db.Users.FindAsync(user.Id);
            Assert.NotNull(dbUser);
            dbUser.IsActive = false;
            await db.SaveChangesAsync();
        }

        // GET now redirects
        var getAfterDeactivate = await client.GetAsync("/account/reset-password?token=" + WebUtility.UrlEncode(protectedToken));
        Assert.Equal(HttpStatusCode.Redirect, getAfterDeactivate.StatusCode);
        Assert.Contains("/account/forgot-password", getAfterDeactivate.Headers.Location?.OriginalString);

        // POST fails (using forgot password page token)
        var newPassword = NewPassword();
        var resetPost = await client.PostAsync("/account/reset-password", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await TestAuth.Token(client, "/account/forgot-password"),
            ["Token"] = protectedToken,
            ["NewPassword"] = newPassword,
            ["ConfirmPassword"] = newPassword
        }));

        Assert.Equal(HttpStatusCode.OK, resetPost.StatusCode);
        var html = await resetPost.Content.ReadAsStringAsync();
        Assert.Contains("Password reset session has expired or is invalid. Please request a new code.", html);
    }

    [Fact]
    public async Task Req19_Inactive_users_password_is_NOT_changed()
    {
        using var client = fixture.NewClient();
        var email = NewEmail();
        var password = NewPassword();
        var user = await CreateTestUserAsync(email, password, isActive: true, emailConfirmed: true);

        string originalPasswordHash;
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var dbUser = await userMgr.FindByIdAsync(user.Id);
            originalPasswordHash = dbUser!.PasswordHash!;
        }

        var protectedToken = await ObtainResetTokenWhileActiveAsync(client, email);

        // Deactivate
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var dbUser = await db.Users.FindAsync(user.Id);
            Assert.NotNull(dbUser);
            dbUser.IsActive = false;
            await db.SaveChangesAsync();
        }

        var newPassword = NewPassword();
        await client.PostAsync("/account/reset-password", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await TestAuth.Token(client, "/account/forgot-password"),
            ["Token"] = protectedToken,
            ["NewPassword"] = newPassword,
            ["ConfirmPassword"] = newPassword
        }));

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var dbUser = await userMgr.FindByIdAsync(user.Id);
            Assert.NotNull(dbUser);
            Assert.Equal(originalPasswordHash, dbUser.PasswordHash);
            Assert.False(await userMgr.CheckPasswordAsync(dbUser, newPassword));
        }
    }

    [Fact]
    public async Task Req20_Reactivating_account_manually_in_test_restores_normal_login_behavior()
    {
        using var client = fixture.NewClient();
        var email = NewEmail();
        var password = NewPassword();
        var user = await CreateTestUserAsync(email, password, isActive: false, emailConfirmed: true);

        // Attempt login while inactive -> blocked
        var initialLogin = await client.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await TestAuth.Token(client, "/account/login"),
            ["Email"] = email,
            ["Password"] = password
        }));
        Assert.Equal(HttpStatusCode.OK, initialLogin.StatusCode);

        // Manually reactivate
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var dbUser = await db.Users.FindAsync(user.Id);
            Assert.NotNull(dbUser);
            dbUser.IsActive = true;
            await db.SaveChangesAsync();
        }

        // Login now succeeds
        var activeLogin = await TestAuth.Login(client, email, password);
        Assert.Equal(HttpStatusCode.Redirect, activeLogin.StatusCode);

        var profile = await client.GetAsync("/account/status");
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
    }

    [Fact]
    public async Task Req21_Inactive_account_cannot_use_verification_flow_to_obtain_authenticated_session()
    {
        using var client = fixture.NewClient();
        var email = NewEmail();
        var password = NewPassword();
        var user = await CreateTestUserAsync(email, password, isActive: true, emailConfirmed: false);

        // Initiate registration OTP
        int challengeId;
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var otpService = scope.ServiceProvider.GetRequiredService<IEmailOtpService>();
            var res = await otpService.SendRegistrationOtpAsync(user);
            Assert.True(res.Success);
            challengeId = res.ChallengeId!.Value;
        }

        var code = TestAuth.LatestOtpFor(email, challengeId);

        // Deactivate user
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var dbUser = await db.Users.FindAsync(user.Id);
            Assert.NotNull(dbUser);
            dbUser.IsActive = false;
            await db.SaveChangesAsync();
        }

        // Verify OTP (using login page token)
        var verifyPost = await client.PostAsync("/account/otp/email", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await TestAuth.Token(client, "/account/login"),
            ["ChallengeId"] = challengeId.ToString(),
            ["Purpose"] = EmailOtpPurpose.Registration.ToString(),
            ["MaskedEmail"] = EmailOtpService.MaskEmail(email),
            ["Code"] = code
        }));

        Assert.Equal(HttpStatusCode.Redirect, verifyPost.StatusCode);
        Assert.Contains("/account/login", verifyPost.Headers.Location?.OriginalString);

        // Email was not confirmed and user is not signed in
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var dbUser = await userMgr.FindByIdAsync(user.Id);
            Assert.NotNull(dbUser);
            Assert.False(dbUser.EmailConfirmed);
        }

        var status = await client.GetAsync("/account/status");
        Assert.Equal(HttpStatusCode.Redirect, status.StatusCode);
        Assert.Contains("/account/login", status.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Req22_Active_registration_behavior_remains_unchanged()
    {
        using var client = fixture.NewClient();
        var email = NewEmail();
        var password = NewPassword();

        var response = await TestAuth.RegisterAndLogin(client, email, password, "U5B Active Registrant");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var profile = await client.GetAsync("/account/status");
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        var html = await profile.Content.ReadAsStringAsync();
        Assert.Contains("U5B Active Registrant", html);
    }

    [Fact]
    public async Task Req23_No_activate_endpoint_exists()
    {
        using var adminClient = await CreateAdminClientAsync();
        var response = await adminClient.PostAsync("/admin/users/any-id/activate", new StringContent(string.Empty));
        Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task Req24_No_deactivate_endpoint_exists()
    {
        using var adminClient = await CreateAdminClientAsync();
        var response = await adminClient.PostAsync("/admin/users/any-id/deactivate", new StringContent(string.Empty));
        Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task Req25_No_IsActive_mutation_route_exists()
    {
        using var adminClient = await CreateAdminClientAsync();
        var response1 = await adminClient.PostAsync("/admin/users/update-status", new StringContent(string.Empty));
        Assert.True(response1.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed);

        var response2 = await adminClient.PostAsync("/admin/users/status", new StringContent(string.Empty));
        Assert.True(response2.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public void Req26_No_migration_added_in_U5B()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var migrations = db.Database.GetMigrations().ToList();
        Assert.Equal("20261014193957_AddUserIsActive", migrations.Last());
    }
}
