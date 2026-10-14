using Microsoft.Playwright;
using Xunit;

namespace JamesThew.Tests;

public class AdminUserStatusBrowserQATests
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
    public async Task PhaseU5A_AdminUserStatus_BrowserQA_AcrossAllViewports()
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

            // 2. Users Directory Verification Across All Viewports
            foreach (var vp in Viewports)
            {
                await page.SetViewportSizeAsync(vp.Width, vp.Height);
                var vpResp = await page.GotoAsync("/admin/users", domReady);
                Assert.Equal(200, vpResp?.Status);

                // Verify status filter select exists
                var statusSelect = page.Locator("select[name='status']");
                Assert.True(await statusSelect.IsVisibleAsync(), $"Status filter select not visible at {vp.Width}x{vp.Height}");

                // Verify Account Status column header is present
                var content = await page.ContentAsync();
                Assert.Contains("Account Status", content);
                Assert.Contains("Active", content);

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

            // 3. User Details Account Status Verification
            await page.SetViewportSizeAsync(1440, 900);
            await page.GotoAsync($"/admin/users?q={server.MemberEmail}", domReady);

            var detailsLink = page.Locator("a:has-text('View Details')").First;
            Assert.True(await detailsLink.IsVisibleAsync());
            await detailsLink.ClickAsync();
            await page.WaitForURLAsync("**/admin/users/*");

            var detailsContent = await page.ContentAsync();

            // Verify Account Status is visible and distinct from Security Status
            Assert.Contains("Account Status", detailsContent);
            Assert.Contains("Account is enabled", detailsContent);
            Assert.Contains("Security Status", detailsContent);

            // Verify NO activate or deactivate buttons exist
            Assert.Equal(0, await page.Locator("button:has-text('Activate')").CountAsync());
            Assert.Equal(0, await page.Locator("button:has-text('Deactivate')").CountAsync());
            Assert.Equal(0, await page.Locator("a:has-text('Activate')").CountAsync());
            Assert.Equal(0, await page.Locator("a:has-text('Deactivate')").CountAsync());

            // 4. Edit User Profile Verification (IsActive must NOT be editable)
            var editLink = page.Locator("a:has-text('Edit Profile')").First;
            Assert.True(await editLink.IsVisibleAsync());
            await editLink.ClickAsync();
            await page.WaitForURLAsync("**/admin/users/*/edit**");

            // Verify there is NO editable input or select for IsActive
            Assert.Equal(0, await page.Locator("input[name='IsActive'], select[name='IsActive']").CountAsync());

            // Verify read-only status badge is visible in header
            var editContent = await page.ContentAsync();
            Assert.Contains("Active", editContent);

            // 5. Back to User Details and verify Role Management section remains intact
            await page.ClickAsync("a:has-text('Back to User Details')");
            await page.WaitForURLAsync("**/admin/users/*");

            var returnDetailsContent = await page.ContentAsync();
            Assert.Contains("Role Management &amp; Governance", returnDetailsContent);
            Assert.Contains("Current Role", returnDetailsContent);
        }
        finally
        {
            await server.DisposeAsync();
        }
    }
}
