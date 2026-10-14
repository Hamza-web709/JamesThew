using Microsoft.Playwright;
using Xunit;

namespace JamesThew.Tests;

public class AdminUserDetailsBrowserQATests
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
    public async Task PhaseU3_AdminUserDetails_BrowserQA_AcrossAllViewports()
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

            // 1. Login as Admin
            await page.GotoAsync("/account/login", domReady);
            await page.FillAsync("input[name='Email']", "admin@jamesthew.com");
            await page.FillAsync("input[name='Password']", "Admin@Pass1234!");
            await page.ClickAsync("form[action*='/account/login'] button[type='submit']");
            await page.WaitForURLAsync("**/admin");

            // 2. Navigate to /admin/users
            var directoryResp = await page.GotoAsync("/admin/users", domReady);
            Assert.Equal(200, directoryResp?.Status);

            // 3. Find and click "View Details"
            var detailsLink = page.Locator("a:has-text('View Details')").First;
            Assert.Equal(1, await detailsLink.CountAsync());
            Assert.True(await detailsLink.IsVisibleAsync());

            await detailsLink.ClickAsync();
            await page.WaitForURLAsync("**/admin/users/*");

            // 4. Assert core UI sections exist on details page
            Assert.Equal(1, await page.Locator("h1.jt-serif").CountAsync());
            Assert.Equal(1, await page.Locator("h2:has-text('Section A')").CountAsync());
            Assert.Equal(1, await page.Locator("h2:has-text('Section B')").CountAsync());
            Assert.Equal(1, await page.Locator("h2:has-text('Section C')").CountAsync());
            Assert.Equal(1, await page.Locator("h2:has-text('Section D')").CountAsync());

            var backLinks = page.Locator("a:has-text('Back to Users')");
            Assert.True(await backLinks.CountAsync() >= 1);

            var userDetailUrl = page.Url;

            // 5. Test across all 7 viewports
            foreach (var vp in Viewports)
            {
                await page.SetViewportSizeAsync(vp.Width, vp.Height);
                var vpResp = await page.GotoAsync(userDetailUrl, domReady);
                Assert.Equal(200, vpResp?.Status);

                Assert.Equal(1, await page.Locator(".jt-admin-shell").CountAsync());
                Assert.Equal(1, await page.Locator(".jt-admin-topbar").CountAsync());
                Assert.True(await page.Locator("h1.jt-serif").IsVisibleAsync(), $"User header not visible at {vp.Width}x{vp.Height}");
                Assert.True(await page.Locator("h2:has-text('Section A')").IsVisibleAsync(), $"Section A not visible at {vp.Width}x{vp.Height}");
                Assert.True(await page.Locator("h2:has-text('Section B')").IsVisibleAsync(), $"Section B not visible at {vp.Width}x{vp.Height}");
                Assert.True(await page.Locator("h2:has-text('Section C')").IsVisibleAsync(), $"Section C not visible at {vp.Width}x{vp.Height}");

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

            // 6. Test Back to Users navigation
            await page.Locator("a:has-text('Back to Users')").First.ClickAsync();
            await page.WaitForURLAsync("**/admin/users**");
            Assert.Contains("/admin/users", page.Url);
        }
        finally
        {
            await server.DisposeAsync();
        }
    }
}
