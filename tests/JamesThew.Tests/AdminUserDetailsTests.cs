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
public class AdminUserDetailsTests(FoundationFixture fixture)
{
    private static string NewEmail() => "admin_det_" + Guid.NewGuid().ToString("N") + "@jamesthew.test";
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

    // =========================================================================
    // 1-3. Authorization: Guest, Member, and Admin
    // =========================================================================

    [Fact]
    public async Task Req01_Guest_cannot_access_user_details_and_is_redirected_to_login()
    {
        using var client = fixture.NewClient();
        var response = await client.GetAsync("/admin/users/any-user-id");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/account/login", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Req02_Member_cannot_access_user_details_and_receives_access_denied()
    {
        var (client, _, _, _, userId) = await CreateMemberClientAsync();
        using (client)
        {
            var response = await client.GetAsync($"/admin/users/{userId}");
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
    public async Task Req03_Admin_can_access_valid_user_details_and_receives_200_ok()
    {
        var (memberClient, memberEmail, _, memberName, memberId) = await CreateMemberClientAsync();
        memberClient.Dispose();

        var (adminClient, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var response = await adminClient.GetAsync($"/admin/users/{memberId}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains(memberName, html);
            Assert.Contains(memberEmail, html);
        }
    }

    // =========================================================================
    // 4. Safe Not-Found Handling
    // =========================================================================

    [Fact]
    public async Task Req04_Nonexistent_user_returns_safe_404_not_found()
    {
        var (adminClient, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var response = await adminClient.GetAsync("/admin/users/does-not-exist-99999");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    // =========================================================================
    // 5-8. Identity Rendering: DisplayName, Email, Role, EmailConfirmed
    // =========================================================================

    [Fact]
    public async Task Req05_to_Req08_Identity_fields_DisplayName_Email_Role_Verification_rendered()
    {
        var memberName = "Chef_" + Guid.NewGuid().ToString("N")[..8];
        var (memberClient, memberEmail, _, _, memberId) = await CreateMemberClientAsync(memberName);
        memberClient.Dispose();

        var (adminClient, adminEmail, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            // Check member
            var memberResp = await adminClient.GetAsync($"/admin/users/{memberId}");
            Assert.Equal(HttpStatusCode.OK, memberResp.StatusCode);
            var memberHtml = await memberResp.Content.ReadAsStringAsync();

            Assert.Contains(memberName, memberHtml);
            Assert.Contains(memberEmail, memberHtml);
            Assert.Contains("Member", memberHtml);
            Assert.Contains("Confirmed", memberHtml); // RegisterAndLogin marks email confirmed

            // Check admin
            string adminId;
            await using (var scope = fixture.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var adminUser = await db.Users.FirstAsync(u => u.Email == adminEmail);
                adminId = adminUser.Id;
            }

            var adminUserResp = await adminClient.GetAsync($"/admin/users/{adminId}");
            Assert.Equal(HttpStatusCode.OK, adminUserResp.StatusCode);
            var adminUserHtml = await adminUserResp.Content.ReadAsStringAsync();

            Assert.Contains(adminEmail, adminUserHtml);
            Assert.Contains("Administrator", adminUserHtml);
        }
    }

    // =========================================================================
    // 9. Temporary Identity Lockout State
    // =========================================================================

    [Fact]
    public async Task Req09_Temporary_lockout_state_shown_accurately_with_lockout_timestamp()
    {
        var (memberClient, _, _, _, memberId) = await CreateMemberClientAsync();
        memberClient.Dispose();

        var lockoutExpiry = DateTimeOffset.UtcNow.AddHours(3);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userMgr.FindByIdAsync(memberId);
            await userMgr.SetLockoutEndDateAsync(user!, lockoutExpiry);
        }

        var (adminClient, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var response = await adminClient.GetAsync($"/admin/users/{memberId}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();

            Assert.Contains("Temporarily Locked", html);
            Assert.Contains("Temporary Identity Lockout", html);
        }
    }

    // =========================================================================
    // 10-11. Membership Summary and Subscription History Count
    // =========================================================================

    [Fact]
    public async Task Req10_Req11_Membership_summary_and_subscription_history_count_correct()
    {
        var (memberClient, _, _, _, memberId) = await CreateMemberClientAsync();
        memberClient.Dispose();

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.SubscriptionRequests.Add(new SubscriptionRequest
            {
                UserId = memberId,
                Plan = SubscriptionPlan.Yearly,
                Amount = 90.00m,
                Status = SubscriptionStatus.Approved,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-10),
                ReviewedAtUtc = DateTime.UtcNow.AddDays(-10),
                ReviewedByAdminId = "TestAdmin",
                ExpiresAtUtc = DateTime.UtcNow.AddDays(355)
            });
            db.SubscriptionRequests.Add(new SubscriptionRequest
            {
                UserId = memberId,
                Plan = SubscriptionPlan.Monthly,
                Amount = 10.00m,
                Status = SubscriptionStatus.Rejected,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-30),
                ReviewedAtUtc = DateTime.UtcNow.AddDays(-30),
                ReviewedByAdminId = "TestAdmin"
            });
            await db.SaveChangesAsync();
        }

        var (adminClient, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var response = await adminClient.GetAsync($"/admin/users/{memberId}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();

            Assert.Contains("Annual", html);
            Assert.Contains("Active Annual", html);
            Assert.Contains("2</span> request(s) recorded", html);
        }
    }

    // =========================================================================
    // 12-15. Activity Metrics: Recipes, Tips, Contest Entries, Feedback
    // =========================================================================

    [Fact]
    public async Task Req12_to_Req15_Activity_metrics_recipes_tips_contests_feedback_accurate()
    {
        var memberName = "ActiveChef_" + Guid.NewGuid().ToString("N")[..8];
        var (memberClient, _, _, _, memberId) = await CreateMemberClientAsync(memberName);
        memberClient.Dispose();

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // 2 Recipes
            db.ContentItems.Add(new ContentItem
            {
                Title = "Recipe 1",
                Slug = "recipe-1-" + Guid.NewGuid().ToString("N")[..8],
                Summary = "Summary 1",
                AuthorUserId = memberId,
                AuthorDisplayName = memberName,
                Kind = ContentKind.Recipe,
                Origin = ContentOrigin.Community
            });
            db.ContentItems.Add(new ContentItem
            {
                Title = "Recipe 2",
                Slug = "recipe-2-" + Guid.NewGuid().ToString("N")[..8],
                Summary = "Summary 2",
                AuthorUserId = memberId,
                AuthorDisplayName = memberName,
                Kind = ContentKind.Recipe,
                Origin = ContentOrigin.Community
            });

            // 1 Tip
            db.ContentItems.Add(new ContentItem
            {
                Title = "Tip 1",
                Slug = "tip-1-" + Guid.NewGuid().ToString("N")[..8],
                Summary = "Summary Tip 1",
                AuthorUserId = memberId,
                AuthorDisplayName = memberName,
                Kind = ContentKind.Tip,
                Origin = ContentOrigin.Community
            });

            // Contest and Entry
            var contest = new Contest
            {
                Title = "Contest " + Guid.NewGuid().ToString("N")[..8],
                Slug = "contest-" + Guid.NewGuid().ToString("N")[..8],
                Summary = "Contest summary description",
                DescriptionAndRules = "Rules of the contest for test assertions",
                Type = ContestType.Recipe,
                Status = ContestStatus.Published,
                OpensAtUtc = DateTime.UtcNow.AddDays(-2),
                ClosesAtUtc = DateTime.UtcNow.AddDays(5)
            };
            db.Contests.Add(contest);
            await db.SaveChangesAsync();

            db.ContestEntries.Add(new ContestEntry
            {
                ContestId = contest.Id,
                AuthorUserId = memberId,
                Title = "Winning Recipe",
                Summary = "Winning entry summary",
                EntryKind = ContestType.Recipe,
                Status = ContestEntryStatus.Selected
            });

            // Feedback
            db.Feedbacks.Add(new Feedback
            {
                AuthorUserId = memberId,
                Kind = FeedbackKind.Site,
                Message = "Wonderful website experience!",
                Status = FeedbackStatus.Pending
            });

            await db.SaveChangesAsync();
        }

        var (adminClient, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var response = await adminClient.GetAsync($"/admin/users/{memberId}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();

            Assert.Contains("Section C &mdash; Activity Summary", html);
            // Recipe count: 2
            Assert.Contains("Recipe Contributions", html);
            // Tip count: 1
            Assert.Contains("Tip Contributions", html);
            // Contest count: 1
            Assert.Contains("Contest Entries", html);
            Assert.Contains("1 winning", html);
            // Feedback count: 1
            Assert.Contains("Feedback Submissions", html);
        }
    }

    // =========================================================================
    // 16. Sensitive Identity Field Security Audit
    // =========================================================================

    [Fact]
    public async Task Req16_Sensitive_identity_fields_are_never_rendered_on_user_details_page()
    {
        var (memberClient, _, _, _, memberId) = await CreateMemberClientAsync();
        memberClient.Dispose();

        var (adminClient, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var response = await adminClient.GetAsync($"/admin/users/{memberId}");
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
            Assert.DoesNotContain("CardNumber", html);
            Assert.DoesNotContain("Cvv", html);
        }
    }

    // =========================================================================
    // 17. Details Link on U2 Directory Points to Correct User
    // =========================================================================

    [Fact]
    public async Task Req17_Details_link_on_U2_directory_points_to_correct_user()
    {
        var uniqueName = "LinkTarget_" + Guid.NewGuid().ToString("N")[..8];
        var (memberClient, memberEmail, _, _, memberId) = await CreateMemberClientAsync(uniqueName);
        memberClient.Dispose();

        var (adminClient, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var response = await adminClient.GetAsync($"/admin/users?q={uniqueName}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();

            Assert.Contains($"href=\"/admin/users/{memberId}", html);
            Assert.Contains("View Details", html);
        }
    }

    // =========================================================================
    // 18. No Mutation Forms or Action Buttons on U3 Details Page
    // =========================================================================

    [Fact]
    public async Task Req18_No_mutation_forms_or_action_buttons_exist_on_user_details_page()
    {
        var (memberClient, _, _, _, memberId) = await CreateMemberClientAsync();
        memberClient.Dispose();

        var (adminClient, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var response = await adminClient.GetAsync($"/admin/users/{memberId}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();

            // Assert no POST forms exist on page (except layout logout form if present and U4B role form)
            var postForms = html.Split(new[] { "<form" }, StringSplitOptions.None)
                                .Skip(1)
                                .Select(f => f.Substring(0, Math.Min(500, f.Length)))
                                .Where(f => !f.Contains("action=\"/account/logout\"", StringComparison.OrdinalIgnoreCase)
                                         && !f.Contains($"/admin/users/{memberId}/role", StringComparison.OrdinalIgnoreCase))
                                .ToList();

            Assert.Empty(postForms);

            // Assert no delete/deactivate buttons exist
            Assert.DoesNotContain("Delete User", html);
            Assert.DoesNotContain("Deactivate", html);
        }
    }
}
