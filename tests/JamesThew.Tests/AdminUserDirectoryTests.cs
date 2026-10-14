using System.Net;
using System.Security.Cryptography;
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
public class AdminUserDirectoryTests(FoundationFixture fixture)
{
    private static string NewEmail() => "admin_dir_" + Guid.NewGuid().ToString("N") + "@jamesthew.test";
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

    private async Task<(HttpClient Client, string Email, string Password, string DisplayName)> CreateMemberClientAsync(string? displayName = null)
    {
        var email = NewEmail();
        var password = NewPassword();
        var name = displayName ?? ("Member_" + Guid.NewGuid().ToString("N")[..8]);
        var client = fixture.NewClient();
        await TestAuth.RegisterAndLogin(client, email, password, name);
        return (client, email, password, name);
    }

    // =========================================================================
    // 1. Authorization: Guest, Member, and Admin
    // =========================================================================

    [Fact]
    public async Task Req01_Guest_cannot_access_admin_users_and_is_redirected_to_login()
    {
        using var client = fixture.NewClient();
        var response = await client.GetAsync("/admin/users");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/account/login", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Req02_Member_cannot_access_admin_users_and_receives_access_denied()
    {
        var (client, _, _, _) = await CreateMemberClientAsync();
        using (client)
        {
            var response = await client.GetAsync("/admin/users");
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
    public async Task Req03_Admin_can_access_admin_users_and_receives_200_ok()
    {
        var (adminClient, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var response = await adminClient.GetAsync("/admin/users");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains("User &amp; Member Directory", html);
        }
    }

    // =========================================================================
    // 2. Directory Rendering: Known Member & Known Admin
    // =========================================================================

    [Fact]
    public async Task Req04_Directory_renders_known_member_and_known_admin()
    {
        var uniqueMemberName = "UniqueChef_" + Guid.NewGuid().ToString("N")[..8];
        var (memberClient, memberEmail, _, _) = await CreateMemberClientAsync(uniqueMemberName);
        memberClient.Dispose();

        var (adminClient, adminEmail, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var response = await adminClient.GetAsync("/admin/users");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains("User &amp; Member Directory", html);
            Assert.Contains("Administrator", html);
            Assert.Contains("Member", html);

            var memberResp = await adminClient.GetAsync($"/admin/users?q={uniqueMemberName}");
            Assert.Equal(HttpStatusCode.OK, memberResp.StatusCode);
            var memberHtml = await memberResp.Content.ReadAsStringAsync();
            Assert.Contains(uniqueMemberName, memberHtml);
            Assert.Contains(memberEmail, memberHtml);
            Assert.Contains("Member", memberHtml);

            var adminUserResp = await adminClient.GetAsync($"/admin/users?q={adminEmail}");
            Assert.Equal(HttpStatusCode.OK, adminUserResp.StatusCode);
            var adminUserHtml = await adminUserResp.Content.ReadAsStringAsync();
            Assert.Contains(adminEmail, adminUserHtml);
            Assert.Contains("Administrator", adminUserHtml);
        }
    }

    // =========================================================================
    // 3. Search: DisplayName and Email
    // =========================================================================

    [Fact]
    public async Task Req05_Search_by_display_name_returns_matching_user()
    {
        var targetName = "SearchTarget_" + Guid.NewGuid().ToString("N")[..8];
        var (memberClient, targetEmail, _, _) = await CreateMemberClientAsync(targetName);
        memberClient.Dispose();

        var (adminClient, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var response = await adminClient.GetAsync($"/admin/users?q={targetName}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();

            Assert.Contains(targetName, html);
            Assert.Contains(targetEmail, html);
        }
    }

    [Fact]
    public async Task Req06_Search_by_email_returns_matching_user()
    {
        var (memberClient, targetEmail, _, targetName) = await CreateMemberClientAsync();
        memberClient.Dispose();

        var (adminClient, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var response = await adminClient.GetAsync($"/admin/users?q={targetEmail}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();

            Assert.Contains(targetName, html);
            Assert.Contains(targetEmail, html);
        }
    }

    [Fact]
    public async Task Req07_Unknown_search_produces_clean_empty_state()
    {
        var (adminClient, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var nonExistentQuery = "NonExistentUserQuery_" + Guid.NewGuid().ToString("N");
            var response = await adminClient.GetAsync($"/admin/users?q={nonExistentQuery}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();

            Assert.Contains("No users match the current search or filters.", html);
            Assert.Contains("Clear All Filters", html);
        }
    }

    // =========================================================================
    // 4. Role Filters: Member and Admin
    // =========================================================================

    [Fact]
    public async Task Req08_Role_filter_isolates_admins_and_members()
    {
        var (adminClient, adminEmail, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var adminResp = await adminClient.GetAsync($"/admin/users?role=admin&q={adminEmail}");
            Assert.Equal(HttpStatusCode.OK, adminResp.StatusCode);
            var adminHtml = await adminResp.Content.ReadAsStringAsync();
            Assert.Contains(adminEmail, adminHtml);
            Assert.Contains("Administrator", adminHtml);

            var memberResp = await adminClient.GetAsync($"/admin/users?role=member&q={adminEmail}");
            Assert.Equal(HttpStatusCode.OK, memberResp.StatusCode);
            var memberHtml = await memberResp.Content.ReadAsStringAsync();
            Assert.DoesNotContain($"<div class=\"text-break\">{adminEmail}</div>", memberHtml);
            Assert.Contains("No users match the current search or filters.", memberHtml);
        }
    }

    // =========================================================================
    // 5. Verification Filters: Verified and Unverified
    // =========================================================================

    [Fact]
    public async Task Req09_Verification_filters_separate_verified_and_unverified()
    {
        var unverifiedEmail = "unverified_" + Guid.NewGuid().ToString("N") + "@jamesthew.test";
        var unverifiedName = "UnverifiedUser_" + Guid.NewGuid().ToString("N")[..6];
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var u = new ApplicationUser
            {
                UserName = unverifiedEmail,
                Email = unverifiedEmail,
                DisplayName = unverifiedName,
                EmailConfirmed = false
            };
            await userMgr.CreateAsync(u, "Pass@123456789!Aa");
            await userMgr.AddToRoleAsync(u, AppRoles.Member);
        }

        var (adminClient, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var unverifiedResp = await adminClient.GetAsync($"/admin/users?verification=unverified&q={unverifiedName}");
            Assert.Equal(HttpStatusCode.OK, unverifiedResp.StatusCode);
            var unverifiedHtml = await unverifiedResp.Content.ReadAsStringAsync();
            Assert.Contains(unverifiedName, unverifiedHtml);
            Assert.Contains("Unverified", unverifiedHtml);

            var verifiedResp = await adminClient.GetAsync($"/admin/users?verification=verified&q={unverifiedName}");
            Assert.Equal(HttpStatusCode.OK, verifiedResp.StatusCode);
            var verifiedHtml = await verifiedResp.Content.ReadAsStringAsync();
            Assert.DoesNotContain(unverifiedEmail, verifiedHtml);
        }
    }

    // =========================================================================
    // 6. Membership Filter: Active and Inactive
    // =========================================================================

    [Fact]
    public async Task Req10_Membership_filter_separates_active_and_inactive_memberships()
    {
        var paidMemberName = "PaidMember_" + Guid.NewGuid().ToString("N")[..6];
        var (client, paidEmail, _, _) = await CreateMemberClientAsync(paidMemberName);
        client.Dispose();

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == paidEmail);
            db.SubscriptionRequests.Add(new SubscriptionRequest
            {
                UserId = user.Id,
                Plan = SubscriptionPlan.Monthly,
                Amount = 10.00m,
                Status = SubscriptionStatus.Approved,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
                ReviewedAtUtc = DateTime.UtcNow.AddDays(-1),
                ReviewedByAdminId = "TestAdmin",
                ExpiresAtUtc = DateTime.UtcNow.AddDays(29)
            });
            await db.SaveChangesAsync();
        }

        var (adminClient, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var activeResp = await adminClient.GetAsync($"/admin/users?membership=active&q={paidMemberName}");
            Assert.Equal(HttpStatusCode.OK, activeResp.StatusCode);
            var activeHtml = await activeResp.Content.ReadAsStringAsync();
            Assert.Contains(paidMemberName, activeHtml);
            Assert.Contains("Active Monthly", activeHtml);

            var inactiveResp = await adminClient.GetAsync($"/admin/users?membership=inactive&q={paidMemberName}");
            Assert.Equal(HttpStatusCode.OK, inactiveResp.StatusCode);
            var inactiveHtml = await inactiveResp.Content.ReadAsStringAsync();
            Assert.DoesNotContain($"<div class=\"fw-bold text-dark\">{paidMemberName}</div>", inactiveHtml);
            Assert.Contains("No users match the current search or filters.", inactiveHtml);
        }
    }

    // =========================================================================
    // 7. Pagination and Link Preservation
    // =========================================================================

    [Fact]
    public async Task Req11_Pagination_controls_preserve_query_parameters()
    {
        var (adminClient, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var response = await adminClient.GetAsync("/admin/users?role=member&verification=verified&page=1");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();

            if (html.Contains("Next &rarr;"))
            {
                Assert.Contains("role=member", html);
                Assert.Contains("verification=verified", html);
            }
        }
    }

    // =========================================================================
    // 8. Sensitive Identity Field Security Audit
    // =========================================================================

    [Fact]
    public async Task Req12_Sensitive_identity_fields_are_never_rendered_in_html()
    {
        var (adminClient, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var response = await adminClient.GetAsync("/admin/users");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();

            Assert.DoesNotContain("PasswordHash", html);
            Assert.DoesNotContain("SecurityStamp", html);
            Assert.DoesNotContain("ConcurrencyStamp", html);
            Assert.DoesNotContain("IsDemoAdminOtpBypass", html);
            Assert.DoesNotContain("CodeHash", html);
            Assert.DoesNotContain("CodeSalt", html);
            Assert.DoesNotContain("NormalizedEmail", html);
            Assert.DoesNotContain("NormalizedUserName", html);
        }
    }

    // =========================================================================
    // 9. No Mutation Endpoints in U2
    // =========================================================================

    [Fact]
    public async Task Req13_No_mutation_endpoints_exist_under_admin_users_in_phase_u2()
    {
        var (adminClient, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var postResp = await adminClient.PostAsync("/admin/users", new StringContent(string.Empty));
            Assert.True(postResp.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed);

            var activateResp = await adminClient.PostAsync("/admin/users/dummy-id/activate", new StringContent(string.Empty));
            Assert.True(activateResp.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed);

            var deleteResp = await adminClient.PostAsync("/admin/users/dummy-id/delete", new StringContent(string.Empty));
            Assert.True(deleteResp.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed);
        }
    }
}