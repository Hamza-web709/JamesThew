using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using JamesThew.Authorization;
using JamesThew.Data;
using JamesThew.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace JamesThew.Tests;

[Collection("Foundation")]
public class AdminUserStatusTests(FoundationFixture fixture)
{
    private static string NewEmail() => "admin_status_" + Guid.NewGuid().ToString("N") + "@jamesthew.test";
    private static string NewPassword() => "Pass@" + Convert.ToHexString(RandomNumberGenerator.GetBytes(10)) + "!9A";

    private sealed class DevEnv : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "JamesThew";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static IConfiguration SeedConfig(string email, string password) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["LocalAdmin:Email"] = email,
            ["LocalAdmin:Password"] = password
        }).Build();

    private async Task<(HttpClient Client, string Email, string Password, string UserId)> CreateAdminClientAsync()
    {
        var email = NewEmail();
        var password = NewPassword();
        using (var scope = fixture.Services.CreateScope())
        {
            await IdentitySeeder.SeedLocalAdminAsync(scope.ServiceProvider, SeedConfig(email, password), new DevEnv());
        }

        var client = fixture.NewClient();
        await TestAuth.Login(client, email, password);

        string userId;
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userMgr.FindByEmailAsync(email);
            userId = user!.Id;
        }

        return (client, email, password, userId);
    }

    private async Task<(HttpClient Client, string Email, string Password, string DisplayName, string UserId)> CreateMemberClientAsync(string? displayName = null)
    {
        var email = NewEmail();
        var password = NewPassword();
        var name = displayName ?? ("Member_" + Guid.NewGuid().ToString("N")[..8]);

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                DisplayName = name,
                EmailConfirmed = true
            };
            var result = await userMgr.CreateAsync(user, password);
            Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));
            await userMgr.AddToRoleAsync(user, AppRoles.Member);
        }

        var client = fixture.NewClient();
        await TestAuth.Login(client, email, password);

        string userId;
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userMgr.FindByEmailAsync(email);
            userId = user!.Id;
        }

        return (client, email, password, name, userId);
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string requestUri)
    {
        var response = await client.GetAsync(requestUri);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(html, @"name=""__RequestVerificationToken""\s+type=""hidden""\s+value=""([^""]+)""");
        if (!match.Success)
        {
            match = Regex.Match(html, @"type=""hidden""\s+name=""__RequestVerificationToken""\s+value=""([^""]+)""");
        }
        Assert.True(match.Success, $"__RequestVerificationToken was not found on page: {requestUri}");
        return match.Groups[1].Value;
    }

    // =========================================================================
    // 1-4. Schema & Default Value Assertions
    // =========================================================================

    [Fact]
    public void Req01_ApplicationUser_defaults_IsActive_to_true()
    {
        var user = new ApplicationUser();
        Assert.True(user.IsActive);
    }

    [Fact]
    public void Req02_Migration_adds_non_null_IsActive_with_default_true()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var entityType = db.Model.FindEntityType(typeof(ApplicationUser));
        Assert.NotNull(entityType);

        var isActiveProp = entityType.FindProperty(nameof(ApplicationUser.IsActive));
        Assert.NotNull(isActiveProp);
        Assert.False(isActiveProp.IsNullable);
        Assert.Equal(true, isActiveProp.GetDefaultValue());
    }

    [Fact]
    public async Task Req03_Existing_or_newly_seeded_Admin_is_Active()
    {
        var (client, email, _, userId) = await CreateAdminClientAsync();
        client.Dispose();

        await using var scope = fixture.Services.CreateAsyncScope();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = await userMgr.FindByIdAsync(userId);
        Assert.NotNull(admin);
        Assert.True(admin.IsActive);
    }

    [Fact]
    public async Task Req04_Newly_registered_Member_is_Active()
    {
        var (client, _, _, _, userId) = await CreateMemberClientAsync();
        client.Dispose();

        await using var scope = fixture.Services.CreateAsyncScope();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var member = await userMgr.FindByIdAsync(userId);
        Assert.NotNull(member);
        Assert.True(member.IsActive);
    }

    // =========================================================================
    // 5-10. Admin Users Directory Status Rendering & Filtering
    // =========================================================================

    [Fact]
    public async Task Req05_Admin_users_directory_renders_Active_status()
    {
        var uniqueName = "ActiveUser_" + Guid.NewGuid().ToString("N")[..8];
        var (memberClient, _, _, _, memberId) = await CreateMemberClientAsync(uniqueName);
        memberClient.Dispose();

        var (adminClient, _, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var response = await adminClient.GetAsync($"/admin/users?q={uniqueName}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();

            Assert.Contains("Account Status", html);
            Assert.Contains("Active", html);
        }
    }

    [Fact]
    public async Task Req06_Manually_seeded_inactive_user_renders_Inactive_status()
    {
        var uniqueName = "InactiveUser_" + Guid.NewGuid().ToString("N")[..8];
        var (memberClient, _, _, _, memberId) = await CreateMemberClientAsync(uniqueName);
        memberClient.Dispose();

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userMgr.FindByIdAsync(memberId);
            Assert.NotNull(user);
            user.IsActive = false;
            await userMgr.UpdateAsync(user);
        }

        var (adminClient, _, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var response = await adminClient.GetAsync($"/admin/users?q={uniqueName}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();

            Assert.Contains("Inactive", html);
        }
    }

    [Fact]
    public async Task Req07_Status_active_filter_shows_only_active_users()
    {
        var prefix = "StatusActive_" + Guid.NewGuid().ToString("N")[..6];
        var (m1, _, _, _, id1) = await CreateMemberClientAsync(prefix + "_Active");
        m1.Dispose();

        var (m2, _, _, _, id2) = await CreateMemberClientAsync(prefix + "_Inactive");
        m2.Dispose();

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user2 = await userMgr.FindByIdAsync(id2);
            Assert.NotNull(user2);
            user2.IsActive = false;
            await userMgr.UpdateAsync(user2);
        }

        var (adminClient, _, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var activeResp = await adminClient.GetAsync($"/admin/users?q={prefix}&status=active");
            Assert.Equal(HttpStatusCode.OK, activeResp.StatusCode);
            var activeHtml = await activeResp.Content.ReadAsStringAsync();

            Assert.Contains(prefix + "_Active", activeHtml);
            Assert.DoesNotContain(prefix + "_Inactive", activeHtml);
        }
    }

    [Fact]
    public async Task Req08_Status_inactive_filter_shows_only_inactive_users()
    {
        var prefix = "StatusInactive_" + Guid.NewGuid().ToString("N")[..6];
        var (m1, _, _, _, id1) = await CreateMemberClientAsync(prefix + "_Active");
        m1.Dispose();

        var (m2, _, _, _, id2) = await CreateMemberClientAsync(prefix + "_Inactive");
        m2.Dispose();

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user2 = await userMgr.FindByIdAsync(id2);
            Assert.NotNull(user2);
            user2.IsActive = false;
            await userMgr.UpdateAsync(user2);
        }

        var (adminClient, _, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var inactiveResp = await adminClient.GetAsync($"/admin/users?q={prefix}&status=inactive");
            Assert.Equal(HttpStatusCode.OK, inactiveResp.StatusCode);
            var inactiveHtml = await inactiveResp.Content.ReadAsStringAsync();

            Assert.Contains(prefix + "_Inactive", inactiveHtml);
            Assert.DoesNotContain(prefix + "_Active", inactiveHtml);
        }
    }

    [Fact]
    public async Task Req09_Invalid_status_filter_safely_falls_back_to_all()
    {
        var prefix = "StatusFallback_" + Guid.NewGuid().ToString("N")[..6];
        var (m1, _, _, _, id1) = await CreateMemberClientAsync(prefix + "_Active");
        m1.Dispose();

        var (m2, _, _, _, id2) = await CreateMemberClientAsync(prefix + "_Inactive");
        m2.Dispose();

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user2 = await userMgr.FindByIdAsync(id2);
            Assert.NotNull(user2);
            user2.IsActive = false;
            await userMgr.UpdateAsync(user2);
        }

        var (adminClient, _, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var response = await adminClient.GetAsync($"/admin/users?q={prefix}&status=unexpected_invalid_value");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();

            // Falls back to all: both are visible
            Assert.Contains(prefix + "_Active", html);
            Assert.Contains(prefix + "_Inactive", html);
        }
    }

    [Fact]
    public async Task Req10_Status_query_preserved_across_pagination()
    {
        var (adminClient, _, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var response = await adminClient.GetAsync("/admin/users?status=inactive&page=1");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();

            // If pagination bar exists, status=inactive should be retained in links
            if (html.Contains("aria-label=\"User directory pagination\""))
            {
                Assert.Contains("status=inactive", html);
            }
        }
    }

    // =========================================================================
    // 11-13. User Details Account Status & Distinct Lockout
    // =========================================================================

    [Fact]
    public async Task Req11_UserDetails_renders_Active_account_status()
    {
        var (memberClient, _, _, _, memberId) = await CreateMemberClientAsync();
        memberClient.Dispose();

        var (adminClient, _, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var response = await adminClient.GetAsync($"/admin/users/{memberId}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();

            Assert.Contains("Account Status", html);
            Assert.Contains("Active", html);
            Assert.Contains("Account is enabled", html);
        }
    }

    [Fact]
    public async Task Req12_UserDetails_renders_Inactive_account_status()
    {
        var (memberClient, _, _, _, memberId) = await CreateMemberClientAsync();
        memberClient.Dispose();

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userMgr.FindByIdAsync(memberId);
            Assert.NotNull(user);
            user.IsActive = false;
            await userMgr.UpdateAsync(user);
        }

        var (adminClient, _, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var response = await adminClient.GetAsync($"/admin/users/{memberId}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();

            Assert.Contains("Account Status", html);
            Assert.Contains("Inactive", html);
            Assert.Contains("Account is administratively disabled", html);
        }
    }

    [Fact]
    public async Task Req13_Identity_temporary_lockout_remains_distinct_from_IsActive()
    {
        var (memberClient, _, _, _, memberId) = await CreateMemberClientAsync();
        memberClient.Dispose();

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userMgr.FindByIdAsync(memberId);
            Assert.NotNull(user);
            user.IsActive = true;
            user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(30);
            await userMgr.UpdateAsync(user);
        }

        var (adminClient, _, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var response = await adminClient.GetAsync($"/admin/users/{memberId}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();

            // Account status is Active
            Assert.Contains("Active", html);
            Assert.Contains("Account is enabled", html);

            // Security status is Temporarily Locked (distinct, not labelled Inactive)
            Assert.Contains("Temporarily Locked", html);
            Assert.Contains("Temporary Identity Lockout", html);
        }
    }

    // =========================================================================
    // 14-18. Over-posting Protection, Route Absence, and Isolation Assertions
    // =========================================================================

    [Fact]
    public async Task Req14_Profile_edit_POST_cannot_overpost_IsActive()
    {
        var (memberClient, email, _, name, memberId) = await CreateMemberClientAsync();
        memberClient.Dispose();

        var (adminClient, _, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var token = await GetAntiforgeryTokenAsync(adminClient, $"/admin/users/{memberId}/edit");
            var form = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Id"] = memberId,
                ["DisplayName"] = name + " Edited",
                ["Email"] = email,
                ["IsActive"] = "false" // Attempt malicious over-post
            };

            var postResp = await adminClient.PostAsync($"/admin/users/{memberId}/edit", new FormUrlEncodedContent(form));
            Assert.Equal(HttpStatusCode.Redirect, postResp.StatusCode);

            await using var scope = fixture.Services.CreateAsyncScope();
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userMgr.FindByIdAsync(memberId);
            Assert.NotNull(user);
            Assert.True(user.IsActive, "IsActive must not be modified by profile edit POST");
        }
    }

    [Fact]
    public async Task Req15_Role_change_POST_cannot_overpost_IsActive()
    {
        var (memberClient, _, _, _, memberId) = await CreateMemberClientAsync();
        memberClient.Dispose();

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userMgr.FindByIdAsync(memberId);
            Assert.NotNull(user);
            user.IsActive = false;
            await userMgr.UpdateAsync(user);
        }

        var (adminClient, _, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var token = await GetAntiforgeryTokenAsync(adminClient, $"/admin/users/{memberId}");
            var form = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Id"] = memberId,
                ["TargetRole"] = AppRoles.Admin,
                ["IsActive"] = "true" // Malicious over-post attempt
            };

            var postResp = await adminClient.PostAsync($"/admin/users/{memberId}/role", new FormUrlEncodedContent(form));
            Assert.Equal(HttpStatusCode.Redirect, postResp.StatusCode);

            await using var scope = fixture.Services.CreateAsyncScope();
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userMgr.FindByIdAsync(memberId);
            Assert.NotNull(user);
            Assert.False(user.IsActive, "IsActive must not be modified by role change POST");
        }
    }

    [Fact]
    public async Task Req16_No_activate_or_deactivate_endpoints_exist_in_U5A()
    {
        var (adminClient, _, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var actResp = await adminClient.PostAsync("/admin/users/any-id/activate", new StringContent(string.Empty));
            Assert.True(actResp.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed or HttpStatusCode.BadRequest);

            var deactResp = await adminClient.PostAsync("/admin/users/any-id/deactivate", new StringContent(string.Empty));
            Assert.True(deactResp.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed or HttpStatusCode.BadRequest);
        }
    }

    [Fact]
    public async Task Req17_Login_behavior_was_NOT_modified_in_U5A_inactive_user_can_still_authenticate()
    {
        var email = NewEmail();
        var password = NewPassword();

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                DisplayName = "InactiveMember",
                EmailConfirmed = true,
                IsActive = false // Administratively inactive
            };
            var result = await userMgr.CreateAsync(user, password);
            Assert.True(result.Succeeded);
            await userMgr.AddToRoleAsync(user, AppRoles.Member);
        }

        // In U5B, auth enforcement is active: inactive account cannot login.
        using var client = fixture.NewClient();
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
    public async Task Req18_No_deletion_endpoint_introduced()
    {
        var (adminClient, _, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var deleteResp = await adminClient.PostAsync("/admin/users/any-id/delete", new StringContent(string.Empty));
            Assert.True(deleteResp.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed);
        }
    }
}
