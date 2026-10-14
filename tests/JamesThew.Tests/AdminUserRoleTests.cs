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
public class AdminUserRoleTests(FoundationFixture fixture)
{
    private static string NewEmail() => "admin_role_" + Guid.NewGuid().ToString("N") + "@jamesthew.test";
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
        return match.Groups[1].Value;
    }

    // =========================================================================
    // 1-2. Guest & Member Access Authorization
    // =========================================================================

    [Fact]
    public async Task Req01_Guest_cannot_POST_role_change_and_is_redirected_to_login()
    {
        var (_, _, _, _, memberId) = await CreateMemberClientAsync();
        var guestClient = fixture.NewClient();

        var form = new Dictionary<string, string>
        {
            ["Id"] = memberId,
            ["TargetRole"] = AppRoles.Admin
        };

        var response = await guestClient.PostAsync($"/admin/users/{memberId}/role", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.ToString();
        Assert.NotNull(location);
        Assert.Contains("/account/login", location, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Req02_Member_cannot_POST_role_change_and_receives_access_denied()
    {
        var (memberClient1, _, _, _, memberId1) = await CreateMemberClientAsync();
        var (memberClient2, _, _, _, memberId2) = await CreateMemberClientAsync();
        memberClient2.Dispose();

        using (memberClient1)
        {
            var token = await GetAntiforgeryTokenAsync(memberClient1, "/");

            var form = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Id"] = memberId2,
                ["TargetRole"] = AppRoles.Admin
            };

            var response = await memberClient1.PostAsync($"/admin/users/{memberId2}/role", new FormUrlEncodedContent(form));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location?.ToString();
            Assert.NotNull(location);
            Assert.Contains("/account/access-denied", location, StringComparison.OrdinalIgnoreCase);
        }
    }

    // =========================================================================
    // 3-5. Member -> Admin Promotion
    // =========================================================================

    [Fact]
    public async Task Req03_to_Req05_Admin_can_promote_Member_to_Admin_and_roles_are_atomic()
    {
        var (memberClient, _, _, _, memberId) = await CreateMemberClientAsync();
        memberClient.Dispose();

        var (adminClient, _, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var token = await GetAntiforgeryTokenAsync(adminClient, $"/admin/users/{memberId}");
            var form = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Id"] = memberId,
                ["TargetRole"] = AppRoles.Admin
            };

            var postResp = await adminClient.PostAsync($"/admin/users/{memberId}/role", new FormUrlEncodedContent(form));
            Assert.Equal(HttpStatusCode.Redirect, postResp.StatusCode);
            Assert.Equal($"/admin/users/{memberId}", postResp.Headers.Location?.OriginalString);

            await using var scope = fixture.Services.CreateAsyncScope();
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userMgr.FindByIdAsync(memberId);
            Assert.NotNull(user);

            var roles = await userMgr.GetRolesAsync(user);
            Assert.Single(roles);
            Assert.Contains(AppRoles.Admin, roles);
            Assert.DoesNotContain(AppRoles.Member, roles);
        }
    }

    // =========================================================================
    // 6-8. Admin -> Member Demotion
    // =========================================================================

    [Fact]
    public async Task Req06_to_Req08_Admin_can_demote_another_Admin_when_another_Admin_remains()
    {
        var (admin1Client, _, _, _) = await CreateAdminClientAsync();
        var (memberClient, _, _, _, promotedAdminId) = await CreateMemberClientAsync();
        memberClient.Dispose();

        using (admin1Client)
        {
            // 1. Promote member to Admin (creates non-demo admin)
            var token1 = await GetAntiforgeryTokenAsync(admin1Client, $"/admin/users/{promotedAdminId}");
            var form1 = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token1,
                ["Id"] = promotedAdminId,
                ["TargetRole"] = AppRoles.Admin
            };
            var promoResp = await admin1Client.PostAsync($"/admin/users/{promotedAdminId}/role", new FormUrlEncodedContent(form1));
            Assert.Equal(HttpStatusCode.Redirect, promoResp.StatusCode);

            // 2. Demote that Admin back to Member (admin1 still remains as admin)
            var token2 = await GetAntiforgeryTokenAsync(admin1Client, $"/admin/users/{promotedAdminId}");
            var form2 = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token2,
                ["Id"] = promotedAdminId,
                ["TargetRole"] = AppRoles.Member
            };

            var postResp = await admin1Client.PostAsync($"/admin/users/{promotedAdminId}/role", new FormUrlEncodedContent(form2));
            Assert.Equal(HttpStatusCode.Redirect, postResp.StatusCode);

            await using var scope = fixture.Services.CreateAsyncScope();
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userMgr.FindByIdAsync(promotedAdminId);
            Assert.NotNull(user);

            var roles = await userMgr.GetRolesAsync(user);
            Assert.Single(roles);
            Assert.Contains(AppRoles.Member, roles);
            Assert.DoesNotContain(AppRoles.Admin, roles);
        }
    }

    // =========================================================================
    // 9. Current Admin Cannot Demote Self
    // =========================================================================

    [Fact]
    public async Task Req09_Current_Admin_cannot_demote_self()
    {
        var (adminClient, _, _, adminId) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var token = await GetAntiforgeryTokenAsync(adminClient, $"/admin/users/{adminId}");
            var form = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Id"] = adminId,
                ["TargetRole"] = AppRoles.Member
            };

            var postResp = await adminClient.PostAsync($"/admin/users/{adminId}/role", new FormUrlEncodedContent(form));
            Assert.Equal(HttpStatusCode.Redirect, postResp.StatusCode);

            // Verify role still Admin
            await using var scope = fixture.Services.CreateAsyncScope();
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userMgr.FindByIdAsync(adminId);
            Assert.NotNull(user);
            Assert.True(await userMgr.IsInRoleAsync(user, AppRoles.Admin));

            // Follow redirect and assert error banner
            var detailsResp = await adminClient.GetAsync($"/admin/users/{adminId}");
            var html = await detailsResp.Content.ReadAsStringAsync();
            Assert.Contains("You cannot revoke your own Administrator role.", html);
        }
    }

    // =========================================================================
    // 10. Demo Admin Cannot Be Demoted
    // =========================================================================

    [Fact]
    public async Task Req10_Demo_Admin_cannot_be_demoted()
    {
        var (admin1Client, _, _, _) = await CreateAdminClientAsync();
        var (demoAdminClient, _, _, demoAdminId) = await CreateAdminClientAsync();
        demoAdminClient.Dispose();

        // Seeded local admin has IsDemoAdminOtpBypass == true
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var demoAdmin = await userMgr.FindByIdAsync(demoAdminId);
            Assert.NotNull(demoAdmin);
            Assert.True(demoAdmin.IsDemoAdminOtpBypass);
        }

        using (admin1Client)
        {
            var token = await GetAntiforgeryTokenAsync(admin1Client, $"/admin/users/{demoAdminId}");
            var form = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Id"] = demoAdminId,
                ["TargetRole"] = AppRoles.Member
            };

            var postResp = await admin1Client.PostAsync($"/admin/users/{demoAdminId}/role", new FormUrlEncodedContent(form));
            Assert.Equal(HttpStatusCode.Redirect, postResp.StatusCode);

            // Verify demo admin is still Admin
            await using (var scope = fixture.Services.CreateAsyncScope())
            {
                var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var demoAdmin = await userMgr.FindByIdAsync(demoAdminId);
                Assert.NotNull(demoAdmin);
                Assert.True(await userMgr.IsInRoleAsync(demoAdmin, AppRoles.Admin));
            }

            // Follow redirect and check error message
            var detailsResp = await admin1Client.GetAsync($"/admin/users/{demoAdminId}");
            var html = await detailsResp.Content.ReadAsStringAsync();
            Assert.Contains("The demo administrator account must retain the Administrator role.", html);
        }
    }

    // =========================================================================
    // 11. Last Remaining Admin Cannot Be Demoted
    // =========================================================================

    [Fact]
    public async Task Req11_Last_remaining_Admin_cannot_be_demoted()
    {
        var (memberClient, _, _, _, targetUserId) = await CreateMemberClientAsync();
        memberClient.Dispose();

        var (callerAdminClient, _, _, _) = await CreateAdminClientAsync();
        using (callerAdminClient)
        {
            // Capture original role state of target user and ALL existing admins in DB BEFORE any mutation
            var originalRolesMap = new Dictionary<string, HashSet<string>>();
            await using (var scope = fixture.Services.CreateAsyncScope())
            {
                var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

                var targetUser = await userMgr.FindByIdAsync(targetUserId);
                Assert.NotNull(targetUser);
                var targetRoles = await userMgr.GetRolesAsync(targetUser);
                originalRolesMap[targetUserId] = new HashSet<string>(targetRoles, StringComparer.OrdinalIgnoreCase);

                var allCurrentAdmins = await userMgr.GetUsersInRoleAsync(AppRoles.Admin);
                foreach (var admin in allCurrentAdmins)
                {
                    var roles = await userMgr.GetRolesAsync(admin);
                    originalRolesMap[admin.Id] = new HashSet<string>(roles, StringComparer.OrdinalIgnoreCase);
                }
            }

            try
            {
                // 1. Promote target user to Admin (so IsDemoAdminOtpBypass is false, isolating last-admin check)
                var token1 = await GetAntiforgeryTokenAsync(callerAdminClient, $"/admin/users/{targetUserId}");
                var form1 = new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = token1,
                    ["Id"] = targetUserId,
                    ["TargetRole"] = AppRoles.Admin
                };
                var resp1 = await callerAdminClient.PostAsync($"/admin/users/{targetUserId}/role", new FormUrlEncodedContent(form1));
                Assert.Equal(HttpStatusCode.Redirect, resp1.StatusCode);

                // 2. Demote all other admins in DB so targetUser is the ONLY admin in DB
                await using (var scope = fixture.Services.CreateAsyncScope())
                {
                    var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                    var allAdmins = await userMgr.GetUsersInRoleAsync(AppRoles.Admin);
                    foreach (var admin in allAdmins.Where(a => a.Id != targetUserId))
                    {
                        await userMgr.RemoveFromRoleAsync(admin, AppRoles.Admin);
                        if (!originalRolesMap[admin.Id].Contains(AppRoles.Member))
                        {
                            await userMgr.AddToRoleAsync(admin, AppRoles.Member);
                        }
                    }
                }

                // 3. Now attempt to demote targetUser (the only admin in the system)
                var token2 = await GetAntiforgeryTokenAsync(callerAdminClient, $"/admin/users/{targetUserId}");
                var form2 = new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = token2,
                    ["Id"] = targetUserId,
                    ["TargetRole"] = AppRoles.Member
                };

                var postResp = await callerAdminClient.PostAsync($"/admin/users/{targetUserId}/role", new FormUrlEncodedContent(form2));
                Assert.Equal(HttpStatusCode.Redirect, postResp.StatusCode);

                // 4. Verify targetUser is STILL Admin
                await using (var scope = fixture.Services.CreateAsyncScope())
                {
                    var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                    var user = await userMgr.FindByIdAsync(targetUserId);
                    Assert.NotNull(user);
                    Assert.True(await userMgr.IsInRoleAsync(user, AppRoles.Admin));
                }

                // 5. Check error banner
                var detailsResp = await callerAdminClient.GetAsync($"/admin/users/{targetUserId}");
                var html = await detailsResp.Content.ReadAsStringAsync();
                Assert.Contains("The system must retain at least one Administrator.", html);
            }
            finally
            {
                // Restore exact prior role state for EVERY user affected by the test
                await using var restoreScope = fixture.Services.CreateAsyncScope();
                var userMgr = restoreScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

                foreach (var (userId, expectedRoles) in originalRolesMap)
                {
                    var user = await userMgr.FindByIdAsync(userId);
                    if (user == null) continue;

                    var currentRoles = new HashSet<string>(await userMgr.GetRolesAsync(user), StringComparer.OrdinalIgnoreCase);

                    // Remove temporary roles not in original state
                    foreach (var r in currentRoles)
                    {
                        if (!expectedRoles.Contains(r))
                        {
                            await userMgr.RemoveFromRoleAsync(user, r);
                        }
                    }

                    // Re-add original roles missing in current state
                    foreach (var r in expectedRoles)
                    {
                        if (!currentRoles.Contains(r))
                        {
                            await userMgr.AddToRoleAsync(user, r);
                        }
                    }
                }
            }

            // Programmatic verification: assert post-cleanup database role integrity
            await using (var verifyScope = fixture.Services.CreateAsyncScope())
            {
                var userMgr = verifyScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                foreach (var (userId, expectedRoles) in originalRolesMap)
                {
                    var user = await userMgr.FindByIdAsync(userId);
                    Assert.NotNull(user);

                    var actualRoles = await userMgr.GetRolesAsync(user);
                    var actualSet = new HashSet<string>(actualRoles, StringComparer.OrdinalIgnoreCase);

                    // Exact match of roles before test
                    Assert.True(expectedRoles.SetEquals(actualSet),
                        $"User {userId} roles [{string.Join(",", actualRoles)}] do not match original [{string.Join(",", expectedRoles)}]");

                    // No user left roleless
                    Assert.NotEmpty(actualRoles);

                    // No unexpected dual-roles if single-role originally
                    if (expectedRoles.Count == 1)
                    {
                        Assert.Single(actualRoles);
                    }
                }
            }
        }
    }

    // =========================================================================
    // 12-14. Validation, 404, and Conflicting Roles Handling
    // =========================================================================

    [Theory]
    [InlineData("SuperAdmin")]
    [InlineData("Moderator")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("")]
    public async Task Req12_Invalid_role_value_is_rejected_safely(string invalidRole)
    {
        var (memberClient, _, _, _, memberId) = await CreateMemberClientAsync();
        memberClient.Dispose();

        var (adminClient, _, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var token = await GetAntiforgeryTokenAsync(adminClient, $"/admin/users/{memberId}");
            var form = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Id"] = memberId,
                ["TargetRole"] = invalidRole
            };

            var postResp = await adminClient.PostAsync($"/admin/users/{memberId}/role", new FormUrlEncodedContent(form));
            Assert.Equal(HttpStatusCode.Redirect, postResp.StatusCode);

            // Follow redirect and check error
            var detailsResp = await adminClient.GetAsync($"/admin/users/{memberId}");
            var html = await detailsResp.Content.ReadAsStringAsync();
            Assert.Contains("Invalid role specified. Only &#x27;Member&#x27; and &#x27;Admin&#x27; roles are permitted.", html);
        }
    }

    [Fact]
    public async Task Req13_Nonexistent_user_returns_404_on_role_POST()
    {
        var (adminClient, _, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var token = await GetAntiforgeryTokenAsync(adminClient, "/admin/users");
            var fakeId = Guid.NewGuid().ToString("N");
            var form = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Id"] = fakeId,
                ["TargetRole"] = AppRoles.Admin
            };

            var postResp = await adminClient.PostAsync($"/admin/users/{fakeId}/role", new FormUrlEncodedContent(form));
            Assert.Equal(HttpStatusCode.NotFound, postResp.StatusCode);
        }
    }

    [Fact]
    public async Task Req14_Target_with_unexpected_multiple_roles_is_not_silently_rewritten()
    {
        var (memberClient, _, _, _, memberId) = await CreateMemberClientAsync();
        memberClient.Dispose();

        // Artificially put target user in both Member and Admin roles
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userMgr.FindByIdAsync(memberId);
            await userMgr.AddToRoleAsync(user!, AppRoles.Admin);
            var roles = await userMgr.GetRolesAsync(user!);
            Assert.Equal(2, roles.Count);
        }

        var (adminClient, _, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var token = await GetAntiforgeryTokenAsync(adminClient, $"/admin/users/{memberId}");
            var form = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Id"] = memberId,
                ["TargetRole"] = AppRoles.Member
            };

            var postResp = await adminClient.PostAsync($"/admin/users/{memberId}/role", new FormUrlEncodedContent(form));
            Assert.Equal(HttpStatusCode.Redirect, postResp.StatusCode);

            // Assert roles were NOT silently rewritten
            await using (var scope = fixture.Services.CreateAsyncScope())
            {
                var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var user = await userMgr.FindByIdAsync(memberId);
                var roles = await userMgr.GetRolesAsync(user!);
                Assert.Equal(2, roles.Count);
            }

            var detailsResp = await adminClient.GetAsync($"/admin/users/{memberId}");
            var html = await detailsResp.Content.ReadAsStringAsync();
            Assert.Contains("Account has an invalid or conflicting role configuration.", html);
        }
    }

    // =========================================================================
    // 15. Role Change Rotates SecurityStamp
    // =========================================================================

    [Fact]
    public async Task Req15_Role_change_rotates_target_user_SecurityStamp()
    {
        var (memberClient, _, _, _, memberId) = await CreateMemberClientAsync();
        memberClient.Dispose();

        string initialSecurityStamp;
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userMgr.FindByIdAsync(memberId);
            initialSecurityStamp = user!.SecurityStamp!;
        }

        var (adminClient, _, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var token = await GetAntiforgeryTokenAsync(adminClient, $"/admin/users/{memberId}");
            var form = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Id"] = memberId,
                ["TargetRole"] = AppRoles.Admin
            };

            var postResp = await adminClient.PostAsync($"/admin/users/{memberId}/role", new FormUrlEncodedContent(form));
            Assert.Equal(HttpStatusCode.Redirect, postResp.StatusCode);

            await using (var scope = fixture.Services.CreateAsyncScope())
            {
                var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var user = await userMgr.FindByIdAsync(memberId);
                Assert.NotEqual(initialSecurityStamp, user!.SecurityStamp);
            }
        }
    }

    // =========================================================================
    // 16. Over-Posting Protection
    // =========================================================================

    [Fact]
    public async Task Req16_Over_posting_sensitive_fields_during_role_change_is_ignored()
    {
        var (memberClient, memberEmail, _, memberName, memberId) = await CreateMemberClientAsync();
        memberClient.Dispose();

        string originalPasswordHash;
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userMgr.FindByIdAsync(memberId);
            originalPasswordHash = user!.PasswordHash!;
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
                // Malicious over-post fields
                ["DisplayName"] = "Hacked Name",
                ["Email"] = "hacked@email.test",
                ["EmailConfirmed"] = "false",
                ["PasswordHash"] = "HackedPasswordHash",
                ["IsDemoAdminOtpBypass"] = "true",
                ["LockoutEnd"] = DateTimeOffset.UtcNow.AddYears(5).ToString("o"),
                ["AccessFailedCount"] = "999"
            };

            var postResp = await adminClient.PostAsync($"/admin/users/{memberId}/role", new FormUrlEncodedContent(form));
            Assert.Equal(HttpStatusCode.Redirect, postResp.StatusCode);

            await using var scope = fixture.Services.CreateAsyncScope();
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userMgr.FindByIdAsync(memberId);
            Assert.NotNull(user);

            // Role updated
            Assert.True(await userMgr.IsInRoleAsync(user, AppRoles.Admin));
            // Sensitive fields untouched
            Assert.Equal(memberName, user.DisplayName);
            Assert.Equal(memberEmail, user.Email);
            Assert.True(user.EmailConfirmed);
            Assert.Equal(originalPasswordHash, user.PasswordHash);
            Assert.False(user.IsDemoAdminOtpBypass);
            Assert.Equal(0, user.AccessFailedCount);
        }
    }

    // =========================================================================
    // 17-18. Post-Redirect-Get & UI Reflection
    // =========================================================================

    [Fact]
    public async Task Req17_to_Req18_Successful_role_change_uses_PRG_and_User_Details_reflects_updated_role()
    {
        var (memberClient, _, _, memberName, memberId) = await CreateMemberClientAsync();
        memberClient.Dispose();

        var (adminClient, _, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var token = await GetAntiforgeryTokenAsync(adminClient, $"/admin/users/{memberId}");
            var form = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Id"] = memberId,
                ["TargetRole"] = AppRoles.Admin
            };

            var postResp = await adminClient.PostAsync($"/admin/users/{memberId}/role", new FormUrlEncodedContent(form));
            Assert.Equal(HttpStatusCode.Redirect, postResp.StatusCode);
            Assert.Equal($"/admin/users/{memberId}", postResp.Headers.Location?.OriginalString);

            // Follow redirect to User Details
            var detailsResp = await adminClient.GetAsync($"/admin/users/{memberId}");
            Assert.Equal(HttpStatusCode.OK, detailsResp.StatusCode);
            var html = await detailsResp.Content.ReadAsStringAsync();

            Assert.Contains("User role updated successfully.", html);
            Assert.Contains("Administrator", html);
            Assert.Contains(memberName, html);
        }
    }

    // =========================================================================
    // 19. No Activate, Deactivate, or Delete Routes
    // =========================================================================

    [Fact]
    public async Task Req19_No_activate_deactivate_or_delete_routes_exist()
    {
        var (adminClient, _, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            var activateResp = await adminClient.PostAsync("/admin/users/any-id/activate", new StringContent(string.Empty));
            Assert.True(activateResp.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed or HttpStatusCode.BadRequest);

            var deactivateResp = await adminClient.PostAsync("/admin/users/any-id/deactivate", new StringContent(string.Empty));
            Assert.True(deactivateResp.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed or HttpStatusCode.BadRequest);

            var deleteResp = await adminClient.PostAsync("/admin/users/any-id/delete", new StringContent(string.Empty));
            Assert.True(deleteResp.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed);
        }
    }

    // =========================================================================
    // 20. Antiforgery Required on POST
    // =========================================================================

    [Fact]
    public async Task Req20_Antiforgery_token_is_required_on_role_POST()
    {
        var (memberClient, _, _, _, memberId) = await CreateMemberClientAsync();
        memberClient.Dispose();

        var (adminClient, _, _, _) = await CreateAdminClientAsync();
        using (adminClient)
        {
            // Submit form WITHOUT __RequestVerificationToken
            var form = new Dictionary<string, string>
            {
                ["Id"] = memberId,
                ["TargetRole"] = AppRoles.Admin
            };

            var postResp = await adminClient.PostAsync($"/admin/users/{memberId}/role", new FormUrlEncodedContent(form));
            Assert.Equal(HttpStatusCode.BadRequest, postResp.StatusCode);
        }
    }
}
