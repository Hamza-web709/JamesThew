using Microsoft.Playwright;
using Xunit;

namespace JamesThew.Tests;

public class AdminUserEditBrowserQATests
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
    public async Task PhaseU4A_AdminUserEdit_BrowserQA_AcrossAllViewports()
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

            // 2. Navigate to /admin/users and find the target member user
            await page.GotoAsync($"/admin/users?q={server.MemberEmail}", domReady);
            var detailsLink = page.Locator("a:has-text('View Details')").First;
            Assert.True(await detailsLink.IsVisibleAsync());
            await detailsLink.ClickAsync();
            await page.WaitForURLAsync("**/admin/users/*");

            // 3. Click Edit Profile on details page
            var editProfileBtn = page.Locator("a:has-text('Edit Profile')").First;
            Assert.True(await editProfileBtn.IsVisibleAsync());
            await editProfileBtn.ClickAsync();
            await page.WaitForURLAsync("**/admin/users/*/edit*");

            // 4. Assert form UI elements exist
            Assert.Equal(1, await page.Locator("h1:has-text('Edit User Profile')").CountAsync());
            Assert.Equal(1, await page.Locator("input[name='DisplayName']").CountAsync());
            Assert.Equal(1, await page.Locator("input[name='Email']").CountAsync());
            Assert.Equal(1, await page.Locator("button[type='submit']:has-text('Save Changes')").CountAsync());
            Assert.Equal(1, await page.Locator("a:has-text('Cancel')").CountAsync());

            var editPageUrl = page.Url;

            // 5. Responsive audit across all 7 viewports
            foreach (var vp in Viewports)
            {
                await page.SetViewportSizeAsync(vp.Width, vp.Height);
                var vpResp = await page.GotoAsync(editPageUrl, domReady);
                Assert.Equal(200, vpResp?.Status);

                Assert.Equal(1, await page.Locator(".jt-admin-shell").CountAsync());
                Assert.Equal(1, await page.Locator(".jt-admin-topbar").CountAsync());
                Assert.True(await page.Locator("input[name='DisplayName']").IsVisibleAsync(), $"DisplayName input not visible at {vp.Width}x{vp.Height}");
                Assert.True(await page.Locator("input[name='Email']").IsVisibleAsync(), $"Email input not visible at {vp.Width}x{vp.Height}");
                Assert.True(await page.Locator("button[type='submit']:has-text('Save Changes')").IsVisibleAsync(), $"Save button not visible at {vp.Width}x{vp.Height}");
                Assert.True(await page.Locator("a:has-text('Cancel')").IsVisibleAsync(), $"Cancel button not visible at {vp.Width}x{vp.Height}");

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

            // 6. Perform DisplayName edit and save
            await page.SetViewportSizeAsync(1440, 900);
            await page.GotoAsync(editPageUrl, domReady);

            var updatedName = "Chef QA " + Guid.NewGuid().ToString("N")[..4];
            await page.FillAsync("input[name='DisplayName']", updatedName);
            await page.ClickAsync("button[type='submit']:has-text('Save Changes')");

            // 7. Verify redirect back to user details and updated value rendered
            await page.WaitForURLAsync("**/admin/users/*");
            var heading = await page.Locator("h1.jt-serif").InnerTextAsync();
            Assert.Contains(updatedName, heading);
            var successAlert = page.Locator(".alert-success");
            Assert.True(await successAlert.IsVisibleAsync());
            Assert.Contains("User profile updated successfully.", await successAlert.InnerTextAsync());
        }
        finally
        {
            await server.DisposeAsync();
        }
    }
}
