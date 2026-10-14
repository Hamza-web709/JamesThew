using JamesThew.Authorization;
using JamesThew.Data;
using JamesThew.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using Xunit;

namespace JamesThew.Tests;

public class AdminUserAccountStatusGovernanceBrowserQATests
{
    private static readonly (int Width, int Height)[] Viewports =
    [
        (1440, 900),
        (1024, 768),
        (768, 1024),
        (430, 932),
        (390, 844)
    ];

    [Fact]
    public async Task PhaseU5C_AccountStatusGovernance_BrowserQA_AcrossViewports()
    {
        var server = new BrowserTestServer();
        await server.InitializeAsync();

        var testMemberEmail = $"qa_member_{server.RunId}@jamesthew.test";
        var testMemberPassword = "Member@Pass1234!";
        string testMemberId;

        // Create isolated test user in the server's test database
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(server.ConnectionString)
            .Options;
        await using (var db = new ApplicationDbContext(dbOptions))
        {
            var hasher = new PasswordHasher<ApplicationUser>();
            var user = new ApplicationUser
            {
                UserName = testMemberEmail,
                NormalizedUserName = testMemberEmail.ToUpperInvariant(),
                Email = testMemberEmail,
                NormalizedEmail = testMemberEmail.ToUpperInvariant(),
                DisplayName = "QA Active Member",
                EmailConfirmed = true,
                IsActive = true,
                SecurityStamp = Guid.NewGuid().ToString()
            };
            user.PasswordHash = hasher.HashPassword(user, testMemberPassword);
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var memberRoleId = await db.Roles.Where(r => r.Name == AppRoles.Member).Select(r => r.Id).SingleAsync();
            db.UserRoles.Add(new IdentityUserRole<string> { UserId = user.Id, RoleId = memberRoleId });
            await db.SaveChangesAsync();

            testMemberId = user.Id;
        }

        try
        {
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                BaseURL = server.ServerAddress,
                IgnoreHTTPSErrors = true,
                ViewportSize = new ViewportSize { Width = 1440, Height = 900 }
            });
            var page = await context.NewPageAsync();
            var domReady = new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 90000
            };

            // Accept any confirmation dialogs automatically
            page.Dialog += async (_, dialog) =>
            {
                await dialog.AcceptAsync();
            };

            // Login as Admin
            await page.GotoAsync("/account/login", domReady);
            await page.FillAsync("input[name='Email']", "admin@jamesthew.com");
            await page.FillAsync("input[name='Password']", "Admin@Pass1234!");
            await page.ClickAsync("form[action*='/account/login'] button[type='submit']");
            await page.WaitForURLAsync("**/admin");

            // =================================================================
            // Scenario A: Admin -> Users -> active Member Details -> Deactivate
            // =================================================================
            await page.GotoAsync($"/admin/users?q={testMemberEmail}", domReady);
            var detailsLink = page.Locator($"a[href*='/admin/users/{testMemberId}']").First;
            Assert.True(await detailsLink.IsVisibleAsync());
            await detailsLink.ClickAsync();
            await page.WaitForURLAsync($"**/admin/users/{testMemberId}**");

            // Verify Section E is present and shows Active status
            var sectionETitle = page.Locator("text=Section E — Account Access & Status Governance");
            Assert.True(await sectionETitle.IsVisibleAsync());

            var deactivateBtn = page.Locator("#btn-deactivate-user");
            Assert.True(await deactivateBtn.IsVisibleAsync());

            // Click Deactivate (dialog accepted automatically)
            await deactivateBtn.ClickAsync();
            await page.WaitForURLAsync($"**/admin/users/{testMemberId}**");
            await page.WaitForSelectorAsync("#btn-activate-user");

            // Verify status becomes Inactive
            var pageContentAfterDeact = await page.ContentAsync();
            Assert.Contains("Inactive", pageContentAfterDeact);
            Assert.Contains("User account deactivated successfully.", pageContentAfterDeact);

            // =================================================================
            // Scenario B: Same User -> Activate -> status becomes Active
            // =================================================================
            var activateBtn = page.Locator("#btn-activate-user");
            Assert.True(await activateBtn.IsVisibleAsync());

            // Click Activate (dialog accepted automatically)
            await activateBtn.ClickAsync();
            await page.WaitForURLAsync($"**/admin/users/{testMemberId}**");
            await page.WaitForSelectorAsync("#btn-deactivate-user");

            // Verify status becomes Active
            var pageContentAfterAct = await page.ContentAsync();
            Assert.Contains("Active", pageContentAfterAct);
            Assert.Contains("User account activated successfully.", pageContentAfterAct);

            // =================================================================
            // Scenario C: Admin views own details -> self-deactivation notice
            // =================================================================
            // Fetch current admin ID
            string currentAdminId;
            await using (var db = new ApplicationDbContext(dbOptions))
            {
                var admin = await db.Users.SingleAsync(u => u.Email == "admin@jamesthew.com");
                currentAdminId = admin.Id;
            }

            await page.GotoAsync($"/admin/users/{currentAdminId}", domReady);
            var selfDeactBtnCount = await page.Locator("#btn-deactivate-user").CountAsync();
            Assert.Equal(0, selfDeactBtnCount);

            var selfNotice = page.Locator("text=You cannot deactivate your own account");
            Assert.True(await selfNotice.IsVisibleAsync());

            // =================================================================
            // Scenario D: Demo admin -> deactivation notice
            // =================================================================
            // admin@jamesthew.com is the demo admin with IsDemoAdminOtpBypass = true
            var demoNotice = page.Locator("text=demo administrator account cannot be deactivated");
            Assert.True(await demoNotice.IsVisibleAsync());

            // =================================================================
            // Viewport Audit across all 5 Viewports
            // =================================================================
            foreach (var vp in Viewports)
            {
                await page.SetViewportSizeAsync(vp.Width, vp.Height);
                await page.GotoAsync($"/admin/users/{testMemberId}", domReady);

                // Verify shell and Section E exist and are visible
                Assert.Equal(1, await page.Locator(".jt-admin-shell").CountAsync());
                Assert.True(await page.Locator("#btn-deactivate-user").IsVisibleAsync());

                // Verify no horizontal overflow
                var overflowIssues = await page.EvaluateAsync<string[]>(@"() => {
                    const viewport = document.documentElement.clientWidth;
                    const issues = [];
                    if (document.documentElement.scrollWidth > viewport + 1) {
                        issues.push('Horizontal overflow: scrollWidth ' + document.documentElement.scrollWidth + ' > viewport ' + viewport);
                    }
                    return issues;
                }");
                Assert.Empty(overflowIssues);
            }
        }
        finally
        {
            await server.DisposeAsync();
        }
    }
}
