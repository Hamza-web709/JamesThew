using Microsoft.Playwright;
using Xunit;

namespace JamesThew.Tests;

public class AdminUserDirectoryBrowserQATests
{
    private static readonly (int Width, int Height)[] Viewports =
    [
        (1440, 900),
        (1366, 768),
        (1280, 720),
        (1024, 768),
        (768, 1024),
        (430, 932),
        (390, 844)
    ];

    [Fact]
    public async Task PhaseU2_AdminUsersDirectory_BrowserQA_AcrossAllViewports()
    {
        var server = new BrowserTestServer();
        await server.InitializeAsync();

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

            // Login as Admin
            await page.GotoAsync("/account/login", domReady);
            await page.FillAsync("input[name='Email']", "admin@jamesthew.com");
            await page.FillAsync("input[name='Password']", "Admin@Pass1234!");
            await page.ClickAsync("form[action*='/account/login'] button[type='submit']");
            await page.WaitForURLAsync("**/admin");

            // Verify Users link in admin navigation
            var usersNavLink = page.Locator(".jt-admin-nav-link:has-text('Users')");
            Assert.Equal(1, await usersNavLink.CountAsync());
            Assert.True(await usersNavLink.IsVisibleAsync());

            // Navigate to /admin/users
            var response = await page.GotoAsync("/admin/users", domReady);
            Assert.Equal(200, response?.Status);

            // Verify core UI elements exist
            Assert.Equal(1, await page.Locator("h1:has-text('User & Member Directory')").CountAsync());
            Assert.Equal(1, await page.Locator("input[name='q']").CountAsync());
            Assert.Equal(1, await page.Locator("select[name='role']").CountAsync());
            Assert.Equal(1, await page.Locator("select[name='verification']").CountAsync());
            Assert.Equal(1, await page.Locator("select[name='membership']").CountAsync());
            Assert.Equal(1, await page.Locator("button[type='submit']:has-text('Apply')").CountAsync());

            // Test across all 7 viewports
            foreach (var vp in Viewports)
            {
                await page.SetViewportSizeAsync(vp.Width, vp.Height);

                var vpResp = await page.GotoAsync("/admin/users", domReady);
                Assert.Equal(200, vpResp?.Status);

                Assert.Equal(1, await page.Locator(".jt-admin-shell").CountAsync());
                Assert.Equal(1, await page.Locator(".jt-admin-topbar").CountAsync());
                Assert.True(await page.Locator("input[name='q']").IsVisibleAsync(), $"Search input not visible at {vp.Width}x{vp.Height}");

                // Horizontal overflow check
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