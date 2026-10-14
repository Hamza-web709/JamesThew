using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using JamesThew.Authorization;
using JamesThew.Controllers;
using JamesThew.Data;
using JamesThew.Models;
using JamesThew.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace JamesThew.Tests;

[Collection("Foundation")]
public class AdminUserAccountStatusGovernanceTests(FoundationFixture fixture)
{
    private static string NewEmail() => "u5c_" + Guid.NewGuid().ToString("N") + "@jamesthew.test";
    private static string NewPassword() => "Pass@" + Convert.ToHexString(RandomNumberGenerator.GetBytes(10)) + "!9A";

    private async Task<ApplicationUser> CreateTestUserAsync(
        string email,
        string password,
        bool isActive = true,
        bool emailConfirmed = true,
        string role = AppRoles.Member,
        bool isDemoAdminBypass = false,
        string? displayName = null)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = displayName ?? ("U5C User " + Guid.NewGuid().ToString("N")[..6]),
            EmailConfirmed = emailConfirmed,
            IsActive = isActive,
            IsDemoAdminOtpBypass = isDemoAdminBypass
        };
        var result = await userMgr.CreateAsync(user, password);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(e => e.Description)));
        await userMgr.AddToRoleAsync(user, role);
        return user;
    }

    private async Task<(HttpClient Client, ApplicationUser AdminUser)> CreateAdminClientAsync()
    {
        var adminEmail = "admin_" + Guid.NewGuid().ToString("N") + "@jamesthew.test";
        var adminPassword = NewPassword();
        var admin = await CreateTestUserAsync(adminEmail, adminPassword, isActive: true, emailConfirmed: true, role: AppRoles.Admin);
        var client = fixture.NewClient();
        await TestAuth.Login(client, adminEmail, adminPassword);
        return (client, admin);
    }

    private async Task<(HttpClient Client, ApplicationUser MemberUser)> CreateMemberClientAsync()
    {
        var memberEmail = NewEmail();
        var memberPassword = NewPassword();
        var member = await CreateTestUserAsync(memberEmail, memberPassword, isActive: true, emailConfirmed: true, role: AppRoles.Member);
        var client = fixture.NewClient();
        await TestAuth.Login(client, memberEmail, memberPassword);
        return (client, member);
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string path)
    {
        var html = await client.GetStringAsync(path);
        var match = Regex.Match(html, @"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""");
        if (!match.Success)
        {
            match = Regex.Match(html, @"value=""([^""]+)""[^>]*name=""__RequestVerificationToken""");
        }
        Assert.True(match.Success, $"Expected an antiforgery token at {path}.");
        return match.Groups[1].Value;
    }

    private static Task<string> GetAdminTokenAsync(HttpClient adminClient) =>
        GetAntiforgeryTokenAsync(adminClient, "/admin/users");

    private static Task<string> GetMemberTokenAsync(HttpClient memberClient) =>
        GetAntiforgeryTokenAsync(memberClient, "/");

    private static Task<string> GetGuestTokenAsync(HttpClient guestClient) =>
        GetAntiforgeryTokenAsync(guestClient, "/account/login");

    // =========================================================================
    // 1-4. Authorization Checks: Guest & Member Blocked
    // =========================================================================

    [Fact]
    public async Task Req01_Guest_cannot_deactivate()
    {
        using var client = fixture.NewClient();
        var target = await CreateTestUserAsync(NewEmail(), NewPassword());

        var response = await client.PostAsync($"/admin/users/{target.Id}/deactivate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await GetGuestTokenAsync(client)
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/account/login", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Req02_Member_cannot_deactivate()
    {
        var (client, _) = await CreateMemberClientAsync();
        var target = await CreateTestUserAsync(NewEmail(), NewPassword());

        var response = await client.PostAsync($"/admin/users/{target.Id}/deactivate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await GetMemberTokenAsync(client)
        }));

        Assert.True(response.StatusCode == HttpStatusCode.Forbidden ||
                    (response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location?.OriginalString.Contains("access-denied", StringComparison.OrdinalIgnoreCase) == true));
    }

    [Fact]
    public async Task Req03_Guest_cannot_activate()
    {
        using var client = fixture.NewClient();
        var target = await CreateTestUserAsync(NewEmail(), NewPassword(), isActive: false);

        var response = await client.PostAsync($"/admin/users/{target.Id}/activate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await GetGuestTokenAsync(client)
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/account/login", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Req04_Member_cannot_activate()
    {
        var (client, _) = await CreateMemberClientAsync();
        var target = await CreateTestUserAsync(NewEmail(), NewPassword(), isActive: false);

        var response = await client.PostAsync($"/admin/users/{target.Id}/activate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await GetMemberTokenAsync(client)
        }));

        Assert.True(response.StatusCode == HttpStatusCode.Forbidden ||
                    (response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location?.OriginalString.Contains("access-denied", StringComparison.OrdinalIgnoreCase) == true));
    }

    // =========================================================================
    // 5-8. Deactivate Active Member: Core Behavior & Field Preservation
    // =========================================================================

    [Fact]
    public async Task Req05_to_08_Admin_can_deactivate_active_Member_and_preserve_role_and_emailConfirmed()
    {
        var (adminClient, _) = await CreateAdminClientAsync();
        var member = await CreateTestUserAsync(NewEmail(), NewPassword(), isActive: true, emailConfirmed: true, role: AppRoles.Member);

        var response = await adminClient.PostAsync($"/admin/users/{member.Id}/deactivate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await GetAdminTokenAsync(adminClient)
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains($"/admin/users/{member.Id}", response.Headers.Location?.OriginalString);

        await using var scope = fixture.Services.CreateAsyncScope();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var updated = await userMgr.FindByIdAsync(member.Id);

        Assert.NotNull(updated);
        Assert.False(updated.IsActive); // Req06
        Assert.True(await userMgr.IsInRoleAsync(updated, AppRoles.Member)); // Req07
        Assert.True(updated.EmailConfirmed); // Req08
    }

    // =========================================================================
    // 9-12. SecurityStamp & OTP Invalidation
    // =========================================================================

    [Fact]
    public async Task Req09_Deactivation_rotates_SecurityStamp()
    {
        var (adminClient, _) = await CreateAdminClientAsync();
        var member = await CreateTestUserAsync(NewEmail(), NewPassword(), isActive: true);

        string originalStamp = member.SecurityStamp!;
        Assert.False(string.IsNullOrWhiteSpace(originalStamp));

        var response = await adminClient.PostAsync($"/admin/users/{member.Id}/deactivate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await GetAdminTokenAsync(adminClient)
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var scope = fixture.Services.CreateAsyncScope();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var updated = await userMgr.FindByIdAsync(member.Id);

        Assert.NotNull(updated);
        Assert.NotEqual(originalStamp, updated.SecurityStamp);
    }

    [Fact]
    public async Task Req10_Deactivation_invalidates_pending_Login_OTP()
    {
        var (adminClient, _) = await CreateAdminClientAsync();
        var email = NewEmail();
        var password = NewPassword();
        var member = await CreateTestUserAsync(email, password, isActive: true, emailConfirmed: true);

        // Initiate Login OTP as active user
        using var userClient = fixture.NewClient();
        var loginResp = await userClient.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await GetGuestTokenAsync(userClient),
            ["Email"] = email,
            ["Password"] = password,
            ["ReturnUrl"] = "/"
        }));
        Assert.Equal(HttpStatusCode.Redirect, loginResp.StatusCode);
        var location = loginResp.Headers.Location?.OriginalString;
        Assert.Contains("/account/otp/email", location);

        // Admin deactivates account
        var deactResp = await adminClient.PostAsync($"/admin/users/{member.Id}/deactivate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await GetAdminTokenAsync(adminClient)
        }));
        Assert.Equal(HttpStatusCode.Redirect, deactResp.StatusCode);

        // Verify challenge in DB has ConsumedAtUtc set
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var challenges = await db.EmailOtpChallenges.Where(c => c.UserId == member.Id && c.Purpose == EmailOtpPurpose.Login).ToListAsync();
            Assert.NotEmpty(challenges);
            Assert.All(challenges, c => Assert.NotNull(c.ConsumedAtUtc));
        }
    }

    [Fact]
    public async Task Req11_Deactivation_invalidates_pending_PasswordReset_OTP()
    {
        var (adminClient, _) = await CreateAdminClientAsync();
        var email = NewEmail();
        var password = NewPassword();
        var member = await CreateTestUserAsync(email, password, isActive: true, emailConfirmed: true);

        // Request PasswordReset OTP while active
        using var userClient = fixture.NewClient();
        var forgotResp = await userClient.PostAsync("/account/forgot-password", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await GetAntiforgeryTokenAsync(userClient, "/account/forgot-password"),
            ["Email"] = email
        }));
        Assert.Equal(HttpStatusCode.Redirect, forgotResp.StatusCode);

        // Admin deactivates account
        var deactResp = await adminClient.PostAsync($"/admin/users/{member.Id}/deactivate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await GetAdminTokenAsync(adminClient)
        }));
        Assert.Equal(HttpStatusCode.Redirect, deactResp.StatusCode);

        // Verify challenges in DB have ConsumedAtUtc set
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var challenges = await db.EmailOtpChallenges.Where(c => c.UserId == member.Id && c.Purpose == EmailOtpPurpose.PasswordReset).ToListAsync();
            Assert.NotEmpty(challenges);
            Assert.All(challenges, c => Assert.NotNull(c.ConsumedAtUtc));
        }
    }

    [Fact]
    public async Task Req12_Old_OTP_remains_invalid_after_reactivation()
    {
        var (adminClient, _) = await CreateAdminClientAsync();
        var email = NewEmail();
        var password = NewPassword();
        var member = await CreateTestUserAsync(email, password, isActive: true, emailConfirmed: true);

        // Request Login OTP while active
        using var userClient = fixture.NewClient();
        var loginResp = await userClient.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await GetGuestTokenAsync(userClient),
            ["Email"] = email,
            ["Password"] = password,
            ["ReturnUrl"] = "/"
        }));
        Assert.Equal(HttpStatusCode.Redirect, loginResp.StatusCode);
        var location = loginResp.Headers.Location?.OriginalString!;
        var challengeId = TestAuth.ChallengeIdFromLocation(location);
        var oldCode = TestAuth.LatestOtpFor(email, challengeId);

        // Grab verification page token while active
        var otpPageToken = await GetAntiforgeryTokenAsync(userClient, location);

        // Admin deactivates account
        await adminClient.PostAsync($"/admin/users/{member.Id}/deactivate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await GetAdminTokenAsync(adminClient)
        }));

        // Admin reactivates account
        await adminClient.PostAsync($"/admin/users/{member.Id}/activate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await GetAdminTokenAsync(adminClient)
        }));

        // User attempts to submit the OLD OTP code
        var otpPostResp = await userClient.PostAsync("/account/otp/email", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = otpPageToken,
            ["ChallengeId"] = challengeId.ToString(),
            ["Purpose"] = EmailOtpPurpose.Login.ToString(),
            ["Email"] = email,
            ["Code"] = oldCode
        }));

        // Old OTP code must FAIL (redirect to login or stay on page with error message)
        Assert.True(otpPostResp.StatusCode == HttpStatusCode.OK || otpPostResp.StatusCode == HttpStatusCode.Redirect);
        if (otpPostResp.StatusCode == HttpStatusCode.Redirect)
        {
            Assert.Contains("/account/login", otpPostResp.Headers.Location?.OriginalString);
        }
        else
        {
            var html = await otpPostResp.Content.ReadAsStringAsync();
            Assert.True(html.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                        html.Contains("incorrect", StringComparison.OrdinalIgnoreCase) ||
                        html.Contains("invalid", StringComparison.OrdinalIgnoreCase));
        }
    }

    // =========================================================================
    // 13-15. Reactivation Core & Fresh Flows
    // =========================================================================

    [Fact]
    public async Task Req13_Admin_can_reactivate_inactive_Member()
    {
        var (adminClient, _) = await CreateAdminClientAsync();
        var member = await CreateTestUserAsync(NewEmail(), NewPassword(), isActive: false, emailConfirmed: true);

        var response = await adminClient.PostAsync($"/admin/users/{member.Id}/activate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await GetAdminTokenAsync(adminClient)
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var scope = fixture.Services.CreateAsyncScope();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var updated = await userMgr.FindByIdAsync(member.Id);
        Assert.NotNull(updated);
        Assert.True(updated.IsActive);
    }

    [Fact]
    public async Task Req14_Reactivated_Member_can_complete_fresh_normal_login_flow()
    {
        var (adminClient, _) = await CreateAdminClientAsync();
        var email = NewEmail();
        var password = NewPassword();
        var member = await CreateTestUserAsync(email, password, isActive: false, emailConfirmed: true);

        // Reactivate
        await adminClient.PostAsync($"/admin/users/{member.Id}/activate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await GetAdminTokenAsync(adminClient)
        }));

        // Fresh login flow
        using var userClient = fixture.NewClient();
        var loginResponse = await TestAuth.Login(userClient, email, password);
        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);

        var statusResponse = await userClient.GetAsync("/account/status");
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        var statusHtml = await statusResponse.Content.ReadAsStringAsync();
        Assert.Contains(email, statusHtml);
    }

    [Fact]
    public async Task Req15_Fresh_OTP_after_reactivation_works()
    {
        var (adminClient, _) = await CreateAdminClientAsync();
        var email = NewEmail();
        var password = NewPassword();
        var member = await CreateTestUserAsync(email, password, isActive: false, emailConfirmed: true);

        // Reactivate
        await adminClient.PostAsync($"/admin/users/{member.Id}/activate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await GetAdminTokenAsync(adminClient)
        }));

        // Request fresh Forgot Password OTP
        using var userClient = fixture.NewClient();
        var forgotResp = await userClient.PostAsync("/account/forgot-password", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await GetAntiforgeryTokenAsync(userClient, "/account/forgot-password"),
            ["Email"] = email
        }));
        Assert.Equal(HttpStatusCode.Redirect, forgotResp.StatusCode);

        int challengeId;
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var ch = await db.EmailOtpChallenges
                .Where(c => c.UserId == member.Id && c.Purpose == EmailOtpPurpose.PasswordReset && c.ConsumedAtUtc == null)
                .OrderByDescending(c => c.CreatedAtUtc)
                .FirstAsync();
            challengeId = ch.Id;
        }

        var freshCode = TestAuth.LatestOtpFor(email, challengeId);
        var verifyUrl = "/account/forgot-password/verify?email=" + WebUtility.UrlEncode(email);
        var verifyResp = await userClient.PostAsync("/account/forgot-password/verify", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await GetAntiforgeryTokenAsync(userClient, verifyUrl),
            ["Email"] = email,
            ["Code"] = freshCode
        }));

        Assert.Equal(HttpStatusCode.Redirect, verifyResp.StatusCode);
        Assert.Contains("token=", verifyResp.Headers.Location?.OriginalString);
    }

    // =========================================================================
    // 16-20. Governance Rules: Self, Demo Admin, Last Active Admin
    // =========================================================================

    [Fact]
    public async Task Req16_Current_Admin_cannot_deactivate_self()
    {
        var (adminClient, currentAdmin) = await CreateAdminClientAsync();

        var response = await adminClient.PostAsync($"/admin/users/{currentAdmin.Id}/deactivate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await GetAdminTokenAsync(adminClient)
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var scope = fixture.Services.CreateAsyncScope();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var reloaded = await userMgr.FindByIdAsync(currentAdmin.Id);
        Assert.NotNull(reloaded);
        Assert.True(reloaded.IsActive);
    }

    [Fact]
    public async Task Req17_Demo_Admin_cannot_be_deactivated()
    {
        var (adminClient, _) = await CreateAdminClientAsync();
        var demoAdmin = await CreateTestUserAsync("demo_admin_" + Guid.NewGuid().ToString("N") + "@jamesthew.test", NewPassword(), isActive: true, role: AppRoles.Admin, isDemoAdminBypass: true);

        var response = await adminClient.PostAsync($"/admin/users/{demoAdmin.Id}/deactivate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await GetAdminTokenAsync(adminClient)
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var scope = fixture.Services.CreateAsyncScope();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var reloaded = await userMgr.FindByIdAsync(demoAdmin.Id);
        Assert.NotNull(reloaded);
        Assert.True(reloaded.IsActive);
    }

    [Fact]
    public async Task Req18_Last_active_Admin_cannot_be_deactivated()
    {
        var (adminClient, _) = await CreateAdminClientAsync();
        var targetAdmin = await CreateTestUserAsync("target_admin_" + Guid.NewGuid().ToString("N") + "@jamesthew.test", NewPassword(), isActive: true, role: AppRoles.Admin);

        List<string> otherActiveAdminIds;
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var adminRoleId = await db.Roles.Where(r => r.Name == AppRoles.Admin).Select(r => r.Id).SingleAsync();
            otherActiveAdminIds = await (from ur in db.UserRoles
                                         join u in db.Users on ur.UserId equals u.Id
                                         where ur.RoleId == adminRoleId && u.Id != targetAdmin.Id && u.IsActive
                                         select u.Id).ToListAsync();
        }

        // Temporarily deactivate all other admins
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var usersToDeactivate = await db.Users.Where(u => otherActiveAdminIds.Contains(u.Id)).ToListAsync();
            foreach (var u in usersToDeactivate)
            {
                u.IsActive = false;
            }
            await db.SaveChangesAsync();
        }

        try
        {
            // Now targetAdmin is the sole active admin
            var response = await adminClient.PostAsync($"/admin/users/{targetAdmin.Id}/deactivate", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = await GetAdminTokenAsync(adminClient)
            }));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            // Verify targetAdmin is STILL active
            await using (var scope = fixture.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var reloaded = await db.Users.AsNoTracking().SingleAsync(u => u.Id == targetAdmin.Id);
                Assert.True(reloaded.IsActive);
            }
        }
        finally
        {
            // Restore all other admins
            await using (var scope = fixture.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var usersToRestore = await db.Users.Where(u => otherActiveAdminIds.Contains(u.Id)).ToListAsync();
                foreach (var u in usersToRestore)
                {
                    u.IsActive = true;
                }
                await db.SaveChangesAsync();
            }
        }
    }

    [Fact]
    public async Task Req19_Inactive_Admin_is_NOT_counted_as_remaining_active_Admin()
    {
        var (adminClient, _) = await CreateAdminClientAsync();
        var targetAdmin = await CreateTestUserAsync("admin_a_" + Guid.NewGuid().ToString("N") + "@jamesthew.test", NewPassword(), isActive: true, role: AppRoles.Admin);
        var inactiveAdmin = await CreateTestUserAsync("admin_b_" + Guid.NewGuid().ToString("N") + "@jamesthew.test", NewPassword(), isActive: false, role: AppRoles.Admin);

        List<string> otherActiveAdminIds;
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var adminRoleId = await db.Roles.Where(r => r.Name == AppRoles.Admin).Select(r => r.Id).SingleAsync();
            otherActiveAdminIds = await (from ur in db.UserRoles
                                         join u in db.Users on ur.UserId equals u.Id
                                         where ur.RoleId == adminRoleId && u.Id != targetAdmin.Id && u.IsActive
                                         select u.Id).ToListAsync();
        }

        // Temporarily deactivate all other active admins
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var usersToDeactivate = await db.Users.Where(u => otherActiveAdminIds.Contains(u.Id)).ToListAsync();
            foreach (var u in usersToDeactivate)
            {
                u.IsActive = false;
            }
            await db.SaveChangesAsync();
        }

        try
        {
            // targetAdmin is active, inactiveAdmin is inactive. No other active admins exist.
            var response = await adminClient.PostAsync($"/admin/users/{targetAdmin.Id}/deactivate", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = await GetAdminTokenAsync(adminClient)
            }));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            // targetAdmin must remain active because inactiveAdmin cannot count as remaining active admin
            await using (var scope = fixture.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var reloaded = await db.Users.AsNoTracking().SingleAsync(u => u.Id == targetAdmin.Id);
                Assert.True(reloaded.IsActive);
            }
        }
        finally
        {
            // Restore other admins
            await using (var scope = fixture.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var usersToRestore = await db.Users.Where(u => otherActiveAdminIds.Contains(u.Id)).ToListAsync();
                foreach (var u in usersToRestore)
                {
                    u.IsActive = true;
                }
                await db.SaveChangesAsync();
            }
        }
    }

    [Fact]
    public async Task Req20_Another_active_Admin_allows_safe_Admin_deactivation()
    {
        var (adminClient, _) = await CreateAdminClientAsync();
        var adminA = await CreateTestUserAsync("admin_x_" + Guid.NewGuid().ToString("N") + "@jamesthew.test", NewPassword(), isActive: true, role: AppRoles.Admin);
        var adminB = await CreateTestUserAsync("admin_y_" + Guid.NewGuid().ToString("N") + "@jamesthew.test", NewPassword(), isActive: true, role: AppRoles.Admin);

        // Since adminB and current admin are both active, deactivating adminA succeeds
        var response = await adminClient.PostAsync($"/admin/users/{adminA.Id}/deactivate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await GetAdminTokenAsync(adminClient)
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var scope = fixture.Services.CreateAsyncScope();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var updated = await userMgr.FindByIdAsync(adminA.Id);
        Assert.NotNull(updated);
        Assert.False(updated.IsActive);
    }

    // =========================================================================
    // 21-22. Role Preservation for Admins
    // =========================================================================

    [Fact]
    public async Task Req21_Deactivating_Admin_preserves_Admin_role()
    {
        var (adminClient, _) = await CreateAdminClientAsync();
        var adminTarget = await CreateTestUserAsync(NewEmail(), NewPassword(), isActive: true, role: AppRoles.Admin);

        var response = await adminClient.PostAsync($"/admin/users/{adminTarget.Id}/deactivate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await GetAdminTokenAsync(adminClient)
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var scope = fixture.Services.CreateAsyncScope();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var updated = await userMgr.FindByIdAsync(adminTarget.Id);
        Assert.NotNull(updated);
        Assert.False(updated.IsActive);
        Assert.True(await userMgr.IsInRoleAsync(updated, AppRoles.Admin));
    }

    [Fact]
    public async Task Req22_Activating_Admin_preserves_Admin_role()
    {
        var (adminClient, _) = await CreateAdminClientAsync();
        var adminTarget = await CreateTestUserAsync(NewEmail(), NewPassword(), isActive: false, role: AppRoles.Admin);

        var response = await adminClient.PostAsync($"/admin/users/{adminTarget.Id}/activate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await GetAdminTokenAsync(adminClient)
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var scope = fixture.Services.CreateAsyncScope();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var updated = await userMgr.FindByIdAsync(adminTarget.Id);
        Assert.NotNull(updated);
        Assert.True(updated.IsActive);
        Assert.True(await userMgr.IsInRoleAsync(updated, AppRoles.Admin));
    }

    // =========================================================================
    // 23-24. Idempotency Checks
    // =========================================================================

    [Fact]
    public async Task Req23_Deactivating_already_inactive_user_is_safe_noop()
    {
        var (adminClient, _) = await CreateAdminClientAsync();
        var target = await CreateTestUserAsync(NewEmail(), NewPassword(), isActive: false);
        var stampBefore = target.SecurityStamp;

        var response = await adminClient.PostAsync($"/admin/users/{target.Id}/deactivate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await GetAdminTokenAsync(adminClient)
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var scope = fixture.Services.CreateAsyncScope();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var updated = await userMgr.FindByIdAsync(target.Id);
        Assert.NotNull(updated);
        Assert.False(updated.IsActive);
        Assert.Equal(stampBefore, updated.SecurityStamp); // No redundant rotation
    }

    [Fact]
    public async Task Req24_Activating_already_active_user_is_safe_noop()
    {
        var (adminClient, _) = await CreateAdminClientAsync();
        var target = await CreateTestUserAsync(NewEmail(), NewPassword(), isActive: true);
        var stampBefore = target.SecurityStamp;

        var response = await adminClient.PostAsync($"/admin/users/{target.Id}/activate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await GetAdminTokenAsync(adminClient)
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var scope = fixture.Services.CreateAsyncScope();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var updated = await userMgr.FindByIdAsync(target.Id);
        Assert.NotNull(updated);
        Assert.True(updated.IsActive);
        Assert.Equal(stampBefore, updated.SecurityStamp); // No redundant rotation
    }

    // =========================================================================
    // 25-28. Over-posting, Antiforgery, 404, ReturnUrl
    // =========================================================================

    [Fact]
    public async Task Req25_Malicious_overpost_cannot_modify_profile_or_security_fields()
    {
        var (adminClient, _) = await CreateAdminClientAsync();
        var originalEmail = NewEmail();
        var member = await CreateTestUserAsync(originalEmail, NewPassword(), isActive: true, emailConfirmed: true, role: AppRoles.Member, displayName: "Legit Name");

        var response = await adminClient.PostAsync($"/admin/users/{member.Id}/deactivate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await GetAdminTokenAsync(adminClient),
            ["DisplayName"] = "Hacked Name",
            ["Email"] = "hacked@evil.test",
            ["EmailConfirmed"] = "false",
            ["Role"] = AppRoles.Admin,
            ["IsDemoAdminOtpBypass"] = "true",
            ["AccessFailedCount"] = "99"
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var scope = fixture.Services.CreateAsyncScope();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var updated = await userMgr.FindByIdAsync(member.Id);
        Assert.NotNull(updated);
        Assert.False(updated.IsActive);
        Assert.Equal("Legit Name", updated.DisplayName);
        Assert.Equal(originalEmail, updated.Email);
        Assert.True(updated.EmailConfirmed);
        Assert.False(updated.IsDemoAdminOtpBypass);
        Assert.Equal(0, updated.AccessFailedCount);
        Assert.True(await userMgr.IsInRoleAsync(updated, AppRoles.Member));
    }

    [Fact]
    public async Task Req26_Antiforgery_required()
    {
        var (adminClient, _) = await CreateAdminClientAsync();
        var member = await CreateTestUserAsync(NewEmail(), NewPassword());

        // Deactivate without token
        var deactResp = await adminClient.PostAsync($"/admin/users/{member.Id}/deactivate", new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.BadRequest, deactResp.StatusCode);

        // Activate without token
        var actResp = await adminClient.PostAsync($"/admin/users/{member.Id}/activate", new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.BadRequest, actResp.StatusCode);
    }

    [Fact]
    public async Task Req27_Nonexistent_target_returns_404()
    {
        var (adminClient, _) = await CreateAdminClientAsync();
        var nonExistentId = Guid.NewGuid().ToString("D");

        var deactResp = await adminClient.PostAsync($"/admin/users/{nonExistentId}/deactivate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await GetAdminTokenAsync(adminClient)
        }));
        Assert.Equal(HttpStatusCode.NotFound, deactResp.StatusCode);

        var actResp = await adminClient.PostAsync($"/admin/users/{nonExistentId}/activate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await GetAdminTokenAsync(adminClient)
        }));
        Assert.Equal(HttpStatusCode.NotFound, actResp.StatusCode);
    }

    [Fact]
    public async Task Req28_Invalid_or_crafted_return_URL_cannot_create_open_redirect()
    {
        var (adminClient, _) = await CreateAdminClientAsync();
        var member = await CreateTestUserAsync(NewEmail(), NewPassword(), isActive: true);

        // Test external domain
        var resp1 = await adminClient.PostAsync($"/admin/users/{member.Id}/deactivate?returnUrl=https://evil.com/phish", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await GetAdminTokenAsync(adminClient)
        }));
        Assert.Equal(HttpStatusCode.Redirect, resp1.StatusCode);
        Assert.DoesNotContain("evil.com", resp1.Headers.Location?.OriginalString);
        Assert.Contains($"/admin/users/{member.Id}", resp1.Headers.Location?.OriginalString);

        // Test scheme-relative URL
        var resp2 = await adminClient.PostAsync($"/admin/users/{member.Id}/activate?returnUrl=//evil.com", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await GetAdminTokenAsync(adminClient)
        }));
        Assert.Equal(HttpStatusCode.Redirect, resp2.StatusCode);
        Assert.DoesNotContain("evil.com", resp2.Headers.Location?.OriginalString);

        // Test safe local return URL
        var resp3 = await adminClient.PostAsync($"/admin/users/{member.Id}/deactivate?returnUrl=/admin/users?page=2", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await GetAdminTokenAsync(adminClient)
        }));
        Assert.Equal(HttpStatusCode.Redirect, resp3.StatusCode);
        Assert.Equal("/admin/users?page=2", resp3.Headers.Location?.OriginalString);
    }

    // =========================================================================
    // 29-31. U5B Inactive Enforcement Compatibility & Reactivation
    // =========================================================================

    [Fact]
    public async Task Req29_U5B_inactive_login_remains_blocked_after_deactivation()
    {
        var (adminClient, _) = await CreateAdminClientAsync();
        var email = NewEmail();
        var password = NewPassword();
        var member = await CreateTestUserAsync(email, password, isActive: true, emailConfirmed: true);

        // Deactivate via Admin
        await adminClient.PostAsync($"/admin/users/{member.Id}/deactivate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await GetAdminTokenAsync(adminClient)
        }));

        // Attempt login
        using var userClient = fixture.NewClient();
        var loginResp = await userClient.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await GetGuestTokenAsync(userClient),
            ["Email"] = email,
            ["Password"] = password
        }));

        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);
        var html = await loginResp.Content.ReadAsStringAsync();
        Assert.Contains("This account is currently inactive. Please contact an administrator.", html);
    }

    [Fact]
    public async Task Req30_U5B_forgot_password_neutral_behavior_remains_intact()
    {
        var (adminClient, _) = await CreateAdminClientAsync();
        var email = NewEmail();
        var member = await CreateTestUserAsync(email, NewPassword(), isActive: true, emailConfirmed: true);

        // Deactivate via Admin
        await adminClient.PostAsync($"/admin/users/{member.Id}/deactivate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await GetAdminTokenAsync(adminClient)
        }));

        // Forgot password
        using var userClient = fixture.NewClient();
        var forgotResp = await userClient.PostAsync("/account/forgot-password", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await GetAntiforgeryTokenAsync(userClient, "/account/forgot-password"),
            ["Email"] = email
        }));

        // Neutral redirect to verify
        Assert.Equal(HttpStatusCode.Redirect, forgotResp.StatusCode);
        Assert.Contains("/account/forgot-password/verify", forgotResp.Headers.Location?.OriginalString);

        // DB check: no OTP challenge generated
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hasChallenges = await db.EmailOtpChallenges.AnyAsync(c => c.UserId == member.Id && c.Purpose == EmailOtpPurpose.PasswordReset && c.CreatedAtUtc > DateTime.UtcNow.AddMinutes(-1));
        Assert.False(hasChallenges);
    }

    [Fact]
    public async Task Req31_Reactivation_restores_login_eligibility()
    {
        var (adminClient, _) = await CreateAdminClientAsync();
        var email = NewEmail();
        var password = NewPassword();
        var member = await CreateTestUserAsync(email, password, isActive: false, emailConfirmed: true);

        // Reactivate via Admin
        var actResp = await adminClient.PostAsync($"/admin/users/{member.Id}/activate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await GetAdminTokenAsync(adminClient)
        }));
        Assert.Equal(HttpStatusCode.Redirect, actResp.StatusCode);

        // Normal login flow
        using var userClient = fixture.NewClient();
        var loginResp = await TestAuth.Login(userClient, email, password);
        Assert.Equal(HttpStatusCode.Redirect, loginResp.StatusCode);

        var statusResp = await userClient.GetAsync("/account/status");
        Assert.Equal(HttpStatusCode.OK, statusResp.StatusCode);
    }

    // =========================================================================
    // 32-33. Boundary Assertions: No Delete Endpoint, No New Migrations
    // =========================================================================

    [Fact]
    public void Req32_No_delete_endpoint_added()
    {
        var adminControllerType = typeof(AdminController);
        var methods = adminControllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        var hasUserDeleteMethod = methods.Any(m =>
            m.Name.Contains("DeleteUser", StringComparison.OrdinalIgnoreCase) ||
            m.GetCustomAttributes().OfType<HttpPostAttribute>().Any(a => a.Template?.Contains("users") == true && a.Template?.Contains("delete") == true));

        Assert.False(hasUserDeleteMethod, "No user deletion endpoints may be added in Phase U5C.");
    }

    [Fact]
    public void Req33_No_migration_added()
    {
        var migrationsDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "JamesThew", "Data", "Migrations");
        var migrationsPath = Path.GetFullPath(migrationsDir);
        Assert.True(Directory.Exists(migrationsPath), $"Migrations directory not found at {migrationsPath}");

        var migrationFiles = Directory.GetFiles(migrationsPath, "*.cs")
            .Where(f => !f.EndsWith("Designer.cs") && !f.EndsWith("ModelSnapshot.cs"))
            .Select(Path.GetFileName)
            .OrderBy(f => f)
            .ToList();

        // The latest migration in U5A/U5B was 20261014193957_AddUserIsActive.cs. No new migrations in U5C.
        var latestMigration = migrationFiles.Last();
        Assert.Contains("AddUserIsActive", latestMigration);
    }

    // =========================================================================
    // Section 23 & 24 Deterministic Tests
    // =========================================================================

    [Fact]
    public async Task SessionRevocation_SecurityStamp_rotated_and_new_login_blocked()
    {
        var (adminClient, _) = await CreateAdminClientAsync();
        var email = NewEmail();
        var password = NewPassword();
        var member = await CreateTestUserAsync(email, password, isActive: true, emailConfirmed: true);

        // Login normal active test user
        using var userClient = fixture.NewClient();
        var loginResponse = await TestAuth.Login(userClient, email, password);
        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);

        string originalStamp = member.SecurityStamp!;

        // Admin deactivates account
        await adminClient.PostAsync($"/admin/users/{member.Id}/deactivate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await GetAdminTokenAsync(adminClient)
        }));

        // Verify SecurityStamp changed
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var updated = await userMgr.FindByIdAsync(member.Id);
            Assert.NotNull(updated);
            Assert.NotEqual(originalStamp, updated.SecurityStamp);
        }

        // Verify new login is immediately blocked
        using var newClient = fixture.NewClient();
        var newLoginResp = await newClient.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await GetGuestTokenAsync(newClient),
            ["Email"] = email,
            ["Password"] = password
        }));

        Assert.Equal(HttpStatusCode.OK, newLoginResp.StatusCode);
        var html = await newLoginResp.Content.ReadAsStringAsync();
        Assert.Contains("This account is currently inactive. Please contact an administrator.", html);
    }

    [Fact]
    public async Task OtpInvalidation_PasswordReset_old_OTP_fails_after_reactivation()
    {
        var (adminClient, _) = await CreateAdminClientAsync();
        var email = NewEmail();
        var member = await CreateTestUserAsync(email, NewPassword(), isActive: true, emailConfirmed: true);

        // Request PasswordReset OTP while active
        using var userClient = fixture.NewClient();
        var forgotResp = await userClient.PostAsync("/account/forgot-password", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await GetAntiforgeryTokenAsync(userClient, "/account/forgot-password"),
            ["Email"] = email
        }));
        Assert.Equal(HttpStatusCode.Redirect, forgotResp.StatusCode);

        int challengeId;
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var ch = await db.EmailOtpChallenges
                .Where(c => c.UserId == member.Id && c.Purpose == EmailOtpPurpose.PasswordReset)
                .OrderByDescending(c => c.CreatedAtUtc)
                .FirstAsync();
            challengeId = ch.Id;
        }
        var oldResetCode = TestAuth.LatestOtpFor(email, challengeId);

        // Admin deactivates member
        await adminClient.PostAsync($"/admin/users/{member.Id}/deactivate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await GetAdminTokenAsync(adminClient)
        }));

        // Admin reactivates member
        await adminClient.PostAsync($"/admin/users/{member.Id}/activate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await GetAdminTokenAsync(adminClient)
        }));

        // User attempts to submit old reset OTP
        var verifyUrl = "/account/forgot-password/verify?email=" + WebUtility.UrlEncode(email);
        var verifyResp = await userClient.PostAsync("/account/forgot-password/verify", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await GetAntiforgeryTokenAsync(userClient, verifyUrl),
            ["Email"] = email,
            ["Code"] = oldResetCode
        }));

        // Must FAIL (not redirect to password reset form with token)
        Assert.Equal(HttpStatusCode.OK, verifyResp.StatusCode);
        var html = await verifyResp.Content.ReadAsStringAsync();
        Assert.Contains("The verification code is incorrect", html, StringComparison.OrdinalIgnoreCase);
    }
}
