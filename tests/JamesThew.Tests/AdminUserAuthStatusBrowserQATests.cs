using JamesThew.Authorization;
using JamesThew.Data;
using JamesThew.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using Xunit;

namespace JamesThew.Tests;

public class AdminUserAuthStatusBrowserQATests
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
    public async Task PhaseU5B_AuthStatus_BrowserQA_AcrossViewports()
    {
        var server = new BrowserTestServer();
        await server.InitializeAsync();

        var inactiveEmail = $"disabled_qa_{server.RunId}@jamesthew.test";
        var inactivePassword = "Disabled@Pass1234!";

        // Create isolated inactive user in the server's test database
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(server.ConnectionString)
            .Options;
        await using (var db = new ApplicationDbContext(dbOptions))
        {
            var hasher = new PasswordHasher<ApplicationUser>();
            var user = new ApplicationUser
            {
                UserName = inactiveEmail,
                NormalizedUserName = inactiveEmail.ToUpperInvariant(),
                Email = inactiveEmail,
                NormalizedEmail = inactiveEmail.ToUpperInvariant(),
                DisplayName = "Inactive QA Member",
                EmailConfirmed = true,
                IsActive = false,
                SecurityStamp = Guid.NewGuid().ToString()
            };
            user.PasswordHash = hasher.HashPassword(user, inactivePassword);
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var memberRoleId = await db.Roles.Where(r => r.Name == AppRoles.Member).Select(r => r.Id).SingleAsync();
            db.UserRoles.Add(new IdentityUserRole<string> { UserId = user.Id, RoleId = memberRoleId });
            await db.SaveChangesAsync();
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

            // Scenario A: Inactive member login attempt across viewports
            foreach (var vp in Viewports)
            {
                await page.SetViewportSizeAsync(vp.Width, vp.Height);
                await page.GotoAsync("/account/login", domReady);

                await page.FillAsync("input[name='Email']", inactiveEmail);
                await page.FillAsync("input[name='Password']", inactivePassword);
                await page.ClickAsync("form[action*='/account/login'] button[type='submit']");

                // Verify stayed on login, not redirected to authenticated route
                Assert.Contains("/account/login", page.Url);

                // Verify inactive error message is visible and readable
                var errorText = await page.Locator(".jt-validation-summary, .alert-danger").InnerTextAsync();
                Assert.Contains("This account is currently inactive. Please contact an administrator.", errorText);

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

            // Scenario C: Inactive account Forgot Password neutral UX across viewports
            foreach (var vp in Viewports)
            {
                await page.SetViewportSizeAsync(vp.Width, vp.Height);
                await page.GotoAsync("/account/forgot-password", domReady);

                await page.FillAsync("input[name='Email']", inactiveEmail);
                await page.ClickAsync("form[action*='/account/forgot-password'] button[type='submit']");

                // Verify neutral redirection to verify page
                await page.WaitForURLAsync("**/account/forgot-password/verify**");
                var visibleText = await page.Locator(".jt-inner-shell").InnerTextAsync();

                // Assert neutral UX, no account state disclosure
                Assert.Contains("verification code", visibleText, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("inactive", visibleText, StringComparison.OrdinalIgnoreCase);

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

            // Scenario B: Active member normal login flow remains functional
            await page.SetViewportSizeAsync(1440, 900);
            await page.GotoAsync("/account/login", domReady);
            await page.FillAsync("input[name='Email']", server.MemberEmail);
            await page.FillAsync("input[name='Password']", "Member@Pass1234!");
            await page.ClickAsync("form[action*='/account/login'] button[type='submit']");

            // Redirects to OTP verification page
            await page.WaitForURLAsync("**/account/otp/email**");
            Assert.Contains("/account/otp/email", page.Url);

            // Complete OTP verification
            await TestAuth.CompleteBrowserEmailOtpAsync(page, server.MemberEmail);

            // Successfully authenticated and redirected away from login/otp
            await page.WaitForURLAsync(url => !url.Contains("/account/login") && !url.Contains("/account/otp/email"));

            // Verify status page access
            await page.GotoAsync("/account/status", domReady);
            var statusContent = await page.ContentAsync();
            Assert.Contains(server.MemberEmail, statusContent);
        }
        finally
        {
            await server.DisposeAsync();
        }
    }
}
