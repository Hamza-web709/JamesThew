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
public class AdminUserEditTests(FoundationFixture fixture)
{
    private static string NewEmail() => "admin_edit_" + Guid.NewGuid().ToString("N") + "@jamesthew.test";
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

    private async Task<(HttpClient Client, string Email, string Password)> CreateAdminClientAsync()
    {
        var email = NewEmail();
        var password = NewPassword();
        using (var scope = fixture.Services.CreateScope())
        {
            await IdentitySeeder.SeedLocalAdminAsync(scope.ServiceProvider, SeedConfig(email, password), new DevEnv());
        }

        var client = fixture.NewClient();
        await TestAuth.Login(client, email, password);
        return (client, email, password);
    }

    private async Task<(HttpClient Client, string Email, string Password, string DisplayName, string UserId)> CreateMemberClientAsync(string? displayName = null)
    {
        var email = NewEmail();
        var password = NewPassword();
        var name = displayName ?? ("Member_" + Guid.NewGuid().ToString("N")[..8]);
        var client = fixture.NewClient();
        await TestAuth.RegisterAndLogin(client, email, password, name);

        string userId;
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == email);
            userId = user.Id;
        }

        return (client, email, password, name, userId);
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string path)
    {
        var html = await client.GetStringAsync(path);
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, $"Expected an antiforgery token at {path}.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    // =========================================================================
    // 1-3. GET Authorization: Guest, Member, and Admin
    // =========================================================================

    [Fact]
    public async Task Req01_Guest_cannot_access_edit_GET_and_is_redirected_to_login()
    {
        using var client = fixture.NewClient();
        var response = await client.GetAsync("/admin/users/any-user-id/edit");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/account/login", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Req02_Member_cannot_access_edit_GET_and_receives_access_denied()
    {
        var (client, _, _, _, userId) = await CreateMemberClientAsync();
        using (client)
        {
            var response = await client.GetAsync($"/admin/users/{userId}/edit");
            if (response.StatusCode == HttpStatusCode.Redirect)
            {
                Assert.Contains("access-denied", response.Headers.Location?.ToString(), StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            }
        }
    }

    [Fact]
    public async Task Req03_Admin_can_access_edit_GET_and_receives_200_ok()
    {
        var (memberClient, _, _, memberName, memberId) = await CreateMemberClientAsync();
        memberClient.Dispose();

        var (adminClient, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var response = await adminClient.GetAsync($"/admin/users/{memberId}/edit");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains("Edit User Profile", html);
            Assert.Contains(memberName, html);
        }
    }

    // =========================================================================
    // 4-5. POST Authorization: Guest, Member
    // =========================================================================

    [Fact]
    public async Task Req04_Guest_cannot_POST_edit_and_is_redirected_to_login()
    {
        using var client = fixture.NewClient();
        var values = new Dictionary<string, string>
        {
            ["Id"] = "target-id",
            ["DisplayName"] = "Hacker",
            ["Email"] = "hacker@test.com"
        };
        var response = await client.PostAsync("/admin/users/target-id/edit", new FormUrlEncodedContent(values));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/account/login", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Req05_Member_cannot_POST_edit_and_receives_access_denied()
    {
        var (memberClient, _, _, _, memberId) = await CreateMemberClientAsync();
        using (memberClient)
        {
            var values = new Dictionary<string, string>
            {
                ["Id"] = memberId,
                ["DisplayName"] = "Attempted Edit",
                ["Email"] = "attempt@test.com"
            };
            var response = await memberClient.PostAsync($"/admin/users/{memberId}/edit", new FormUrlEncodedContent(values));
            if (response.StatusCode == HttpStatusCode.Redirect)
            {
                Assert.Contains("access-denied", response.Headers.Location?.ToString(), StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            }
        }
    }

    // =========================================================================
    // 6-8. DisplayName Updates: Valid, Whitespace, Overlength
    // =========================================================================

    [Fact]
    public async Task Req06_Admin_can_update_DisplayName_successfully()
    {
        var (memberClient, memberEmail, _, initialName, memberId) = await CreateMemberClientAsync();
        memberClient.Dispose();

        var (adminClient, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var token = await GetAntiforgeryTokenAsync(adminClient, $"/admin/users/{memberId}/edit");
            var newName = "UpdatedChef_" + Guid.NewGuid().ToString("N")[..6];

            var form = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Id"] = memberId,
                ["DisplayName"] = newName,
                ["Email"] = memberEmail
            };

            var postResp = await adminClient.PostAsync($"/admin/users/{memberId}/edit", new FormUrlEncodedContent(form));
            Assert.Equal(HttpStatusCode.Redirect, postResp.StatusCode);

            await using var scope = fixture.Services.CreateAsyncScope();
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var updatedUser = await userMgr.FindByIdAsync(memberId);
            Assert.NotNull(updatedUser);
            Assert.Equal(newName, updatedUser.DisplayName);
            Assert.Equal(memberEmail, updatedUser.Email);
        }
    }

    [Fact]
    public async Task Req07_Whitespace_DisplayName_is_rejected()
    {
        var (memberClient, memberEmail, _, initialName, memberId) = await CreateMemberClientAsync();
        memberClient.Dispose();

        var (adminClient, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var token = await GetAntiforgeryTokenAsync(adminClient, $"/admin/users/{memberId}/edit");

            var form = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Id"] = memberId,
                ["DisplayName"] = "   ",
                ["Email"] = memberEmail
            };

            var postResp = await adminClient.PostAsync($"/admin/users/{memberId}/edit", new FormUrlEncodedContent(form));
            Assert.Equal(HttpStatusCode.OK, postResp.StatusCode);
            var html = await postResp.Content.ReadAsStringAsync();
            Assert.Contains("Display Name is required and cannot be empty or whitespace.", html);

            await using var scope = fixture.Services.CreateAsyncScope();
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userMgr.FindByIdAsync(memberId);
            Assert.Equal(initialName, user!.DisplayName);
        }
    }

    [Fact]
    public async Task Req08_Overlength_DisplayName_is_rejected()
    {
        var (memberClient, memberEmail, _, initialName, memberId) = await CreateMemberClientAsync();
        memberClient.Dispose();

        var (adminClient, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var token = await GetAntiforgeryTokenAsync(adminClient, $"/admin/users/{memberId}/edit");
            var tooLong = new string('A', 101);

            var form = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Id"] = memberId,
                ["DisplayName"] = tooLong,
                ["Email"] = memberEmail
            };

            var postResp = await adminClient.PostAsync($"/admin/users/{memberId}/edit", new FormUrlEncodedContent(form));
            Assert.Equal(HttpStatusCode.OK, postResp.StatusCode);
            var html = await postResp.Content.ReadAsStringAsync();
            Assert.Contains("Display Name cannot exceed 100 characters.", html);

            await using var scope = fixture.Services.CreateAsyncScope();
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userMgr.FindByIdAsync(memberId);
            Assert.Equal(initialName, user!.DisplayName);
        }
    }

    // =========================================================================
    // 9-12. Email Updates: Synchronization, Normalization
    // =========================================================================

    [Fact]
    public async Task Req09_to_Req12_Valid_Email_update_synchronizes_UserName_and_normalizations()
    {
        var (memberClient, _, _, memberName, memberId) = await CreateMemberClientAsync();
        memberClient.Dispose();

        var (adminClient, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var token = await GetAntiforgeryTokenAsync(adminClient, $"/admin/users/{memberId}/edit");
            var newEmail = "updated_" + Guid.NewGuid().ToString("N")[..8] + "@jamesthew.test";

            var form = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Id"] = memberId,
                ["DisplayName"] = memberName,
                ["Email"] = newEmail
            };

            var postResp = await adminClient.PostAsync($"/admin/users/{memberId}/edit", new FormUrlEncodedContent(form));
            Assert.Equal(HttpStatusCode.Redirect, postResp.StatusCode);

            await using var scope = fixture.Services.CreateAsyncScope();
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var updatedUser = await userMgr.FindByIdAsync(memberId);
            Assert.NotNull(updatedUser);
            // Email and UserName stay synchronized
            Assert.Equal(newEmail, updatedUser.Email);
            Assert.Equal(newEmail, updatedUser.UserName);
            // Normalizations maintained via Identity
            Assert.Equal(userMgr.NormalizeEmail(newEmail), updatedUser.NormalizedEmail);
            Assert.Equal(userMgr.NormalizeName(newEmail), updatedUser.NormalizedUserName);
            // EmailConfirmed reset
            Assert.False(updatedUser.EmailConfirmed);
        }
    }

    // =========================================================================
    // 13-14. Duplicate and Malformed Email Rejected
    // =========================================================================

    [Fact]
    public async Task Req13_Duplicate_Email_is_rejected_with_validation_message()
    {
        var (user1Client, user1Email, _, _, _) = await CreateMemberClientAsync();
        user1Client.Dispose();

        var (user2Client, user2Email, _, user2Name, user2Id) = await CreateMemberClientAsync();
        user2Client.Dispose();

        var (adminClient, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var token = await GetAntiforgeryTokenAsync(adminClient, $"/admin/users/{user2Id}/edit");

            var form = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Id"] = user2Id,
                ["DisplayName"] = user2Name,
                ["Email"] = user1Email // duplicate
            };

            var postResp = await adminClient.PostAsync($"/admin/users/{user2Id}/edit", new FormUrlEncodedContent(form));
            Assert.Equal(HttpStatusCode.OK, postResp.StatusCode);
            var html = await postResp.Content.ReadAsStringAsync();
            Assert.Contains("Another account is already registered with this email address.", html);

            await using var scope = fixture.Services.CreateAsyncScope();
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userMgr.FindByIdAsync(user2Id);
            Assert.Equal(user2Email, user!.Email);
        }
    }

    [Fact]
    public async Task Req14_Malformed_Email_is_rejected()
    {
        var (memberClient, memberEmail, _, memberName, memberId) = await CreateMemberClientAsync();
        memberClient.Dispose();

        var (adminClient, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var token = await GetAntiforgeryTokenAsync(adminClient, $"/admin/users/{memberId}/edit");

            var form = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Id"] = memberId,
                ["DisplayName"] = memberName,
                ["Email"] = "not-an-email-at-all"
            };

            var postResp = await adminClient.PostAsync($"/admin/users/{memberId}/edit", new FormUrlEncodedContent(form));
            Assert.Equal(HttpStatusCode.OK, postResp.StatusCode);
            var html = await postResp.Content.ReadAsStringAsync();
            Assert.Contains("Please enter a valid email address.", html);

            await using var scope = fixture.Services.CreateAsyncScope();
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userMgr.FindByIdAsync(memberId);
            Assert.Equal(memberEmail, user!.Email);
        }
    }

    // =========================================================================
    // 15-16. Post-Redirect-Get and Safe Not-Found Handling
    // =========================================================================

    [Fact]
    public async Task Req15_Successful_update_uses_post_redirect_get()
    {
        var (memberClient, memberEmail, _, memberName, memberId) = await CreateMemberClientAsync();
        memberClient.Dispose();

        var (adminClient, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var token = await GetAntiforgeryTokenAsync(adminClient, $"/admin/users/{memberId}/edit");
            var updatedName = "PRGName_" + Guid.NewGuid().ToString("N")[..6];

            var form = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Id"] = memberId,
                ["DisplayName"] = updatedName,
                ["Email"] = memberEmail,
                ["ReturnUrl"] = $"/admin/users/{memberId}"
            };

            var postResp = await adminClient.PostAsync($"/admin/users/{memberId}/edit", new FormUrlEncodedContent(form));
            Assert.Equal(HttpStatusCode.Redirect, postResp.StatusCode);
            Assert.Contains($"/admin/users/{memberId}", postResp.Headers.Location?.ToString());
        }
    }

    [Fact]
    public async Task Req16_Nonexistent_user_returns_404_on_GET_and_POST()
    {
        var (adminClient, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var getResp = await adminClient.GetAsync("/admin/users/ghost-user-99999/edit");
            Assert.Equal(HttpStatusCode.NotFound, getResp.StatusCode);

            // POST to nonexistent user
            var form = new Dictionary<string, string>
            {
                ["Id"] = "ghost-user-99999",
                ["DisplayName"] = "Ghost",
                ["Email"] = "ghost@test.com"
            };
            // Note: need token from valid page
            var token = await GetAntiforgeryTokenAsync(adminClient, "/admin/users");
            form["__RequestVerificationToken"] = token;

            var postResp = await adminClient.PostAsync("/admin/users/ghost-user-99999/edit", new FormUrlEncodedContent(form));
            Assert.Equal(HttpStatusCode.NotFound, postResp.StatusCode);
        }
    }

    // =========================================================================
    // 17-19. Over-posting Protection: Role, Demo Bypass, Sensitive Fields
    // =========================================================================

    [Fact]
    public async Task Req17_to_Req19_Sensitive_fields_and_roles_cannot_be_over_posted()
    {
        var (memberClient, memberEmail, _, memberName, memberId) = await CreateMemberClientAsync();
        memberClient.Dispose();

        string originalPasswordHash;
        string originalSecurityStamp;
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userMgr.FindByIdAsync(memberId);
            originalPasswordHash = user!.PasswordHash!;
            originalSecurityStamp = user.SecurityStamp!;
        }

        var (adminClient, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var token = await GetAntiforgeryTokenAsync(adminClient, $"/admin/users/{memberId}/edit");

            var form = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Id"] = memberId,
                ["DisplayName"] = "Overpost Tester",
                ["Email"] = memberEmail,
                // Over-post attack payload
                ["Role"] = AppRoles.Admin,
                ["CurrentRole"] = AppRoles.Admin,
                ["EmailConfirmed"] = "true",
                ["IsDemoAdminOtpBypass"] = "true",
                ["IsDemoAdmin"] = "true",
                ["PasswordHash"] = "HackedPasswordHash",
                ["SecurityStamp"] = "HackedSecurityStamp",
                ["LockoutEnd"] = DateTimeOffset.UtcNow.AddYears(10).ToString("o"),
                ["AccessFailedCount"] = "99"
            };

            var postResp = await adminClient.PostAsync($"/admin/users/{memberId}/edit", new FormUrlEncodedContent(form));
            Assert.Equal(HttpStatusCode.Redirect, postResp.StatusCode);

            await using var scope = fixture.Services.CreateAsyncScope();
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userMgr.FindByIdAsync(memberId);
            Assert.NotNull(user);

            // DisplayName updated
            Assert.Equal("Overpost Tester", user.DisplayName);
            // Sensitive fields untouched
            Assert.Equal(originalPasswordHash, user.PasswordHash);
            Assert.Equal(originalSecurityStamp, user.SecurityStamp);
            Assert.False(user.IsDemoAdminOtpBypass);
            Assert.Equal(0, user.AccessFailedCount);

            // Role untouched: member still has only Member role
            var roles = await userMgr.GetRolesAsync(user);
            Assert.Single(roles);
            Assert.Contains(AppRoles.Member, roles);
            Assert.DoesNotContain(AppRoles.Admin, roles);
        }
    }

    // =========================================================================
    // 20. SecurityStamp Behavior on Email vs DisplayName
    // =========================================================================

    [Fact]
    public async Task Req20_SecurityStamp_updates_on_email_change_and_remains_on_name_only_change()
    {
        var (memberClient, memberEmail, _, memberName, memberId) = await CreateMemberClientAsync();
        memberClient.Dispose();

        string initialSecurityStamp;
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userMgr.FindByIdAsync(memberId);
            initialSecurityStamp = user!.SecurityStamp!;
        }

        var (adminClient, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            // 1. Name-only change
            var token1 = await GetAntiforgeryTokenAsync(adminClient, $"/admin/users/{memberId}/edit");
            var form1 = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token1,
                ["Id"] = memberId,
                ["DisplayName"] = "Name Only Change",
                ["Email"] = memberEmail
            };
            var resp1 = await adminClient.PostAsync($"/admin/users/{memberId}/edit", new FormUrlEncodedContent(form1));
            Assert.Equal(HttpStatusCode.Redirect, resp1.StatusCode);

            await using (var scope = fixture.Services.CreateAsyncScope())
            {
                var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var user = await userMgr.FindByIdAsync(memberId);
                // SecurityStamp preserved on DisplayName only change
                Assert.Equal(initialSecurityStamp, user!.SecurityStamp);
            }

            // 2. Email change
            var newEmail = "sec_stamp_" + Guid.NewGuid().ToString("N")[..8] + "@jamesthew.test";
            var token2 = await GetAntiforgeryTokenAsync(adminClient, $"/admin/users/{memberId}/edit");
            var form2 = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token2,
                ["Id"] = memberId,
                ["DisplayName"] = "Name Only Change",
                ["Email"] = newEmail
            };
            var resp2 = await adminClient.PostAsync($"/admin/users/{memberId}/edit", new FormUrlEncodedContent(form2));
            Assert.Equal(HttpStatusCode.Redirect, resp2.StatusCode);

            await using (var scope = fixture.Services.CreateAsyncScope())
            {
                var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var user = await userMgr.FindByIdAsync(memberId);
                // SecurityStamp refreshed on email change
                Assert.NotEqual(initialSecurityStamp, user!.SecurityStamp);
            }
        }
    }

    // =========================================================================
    // 21. No Role Mutation, Deactivation, or Delete Routes
    // =========================================================================

    [Fact]
    public async Task Req21_No_routes_exist_for_role_mutation_deactivation_or_deletion()
    {
        var (adminClient, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var roleResp = await adminClient.PostAsync("/admin/users/any-id/role", new StringContent(string.Empty));
            Assert.True(roleResp.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed);

            var deactResp = await adminClient.PostAsync("/admin/users/any-id/deactivate", new StringContent(string.Empty));
            Assert.True(deactResp.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed);

            var deleteResp = await adminClient.PostAsync("/admin/users/any-id/delete", new StringContent(string.Empty));
            Assert.True(deleteResp.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed);
        }
    }

    // =========================================================================
    // 22. User Details Page Reflects Updated DisplayName and Email
    // =========================================================================

    [Fact]
    public async Task Req22_User_Details_page_reflects_updated_DisplayName_and_Email()
    {
        var (memberClient, _, _, _, memberId) = await CreateMemberClientAsync();
        memberClient.Dispose();

        var (adminClient, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var token = await GetAntiforgeryTokenAsync(adminClient, $"/admin/users/{memberId}/edit");
            var finalName = "FinalVerified_" + Guid.NewGuid().ToString("N")[..6];
            var finalEmail = "final_" + Guid.NewGuid().ToString("N")[..8] + "@jamesthew.test";

            var form = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Id"] = memberId,
                ["DisplayName"] = finalName,
                ["Email"] = finalEmail
            };

            var postResp = await adminClient.PostAsync($"/admin/users/{memberId}/edit", new FormUrlEncodedContent(form));
            Assert.Equal(HttpStatusCode.Redirect, postResp.StatusCode);

            // Follow redirect to UserDetails
            var detailsResp = await adminClient.GetAsync($"/admin/users/{memberId}");
            Assert.Equal(HttpStatusCode.OK, detailsResp.StatusCode);
            var html = await detailsResp.Content.ReadAsStringAsync();

            Assert.Contains(finalName, html);
            Assert.Contains(finalEmail, html);
            Assert.Contains("Unverified", html); // Reset to unverified because email changed
        }
    }
}
