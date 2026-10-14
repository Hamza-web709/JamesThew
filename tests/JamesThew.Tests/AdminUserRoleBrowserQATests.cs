using Microsoft.Playwright;
using Xunit;

namespace JamesThew.Tests;

public class AdminUserRoleBrowserQATests
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
    public async Task PhaseU4B_AdminUserRole_BrowserQA_AcrossAllViewports()
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

            // Automatically accept browser confirmation dialogs
            page.Dialog += async (_, dialog) =>
            {
                await dialog.AcceptAsync();
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
            var memberDetailsUrl = page.Url;

            // 3. Responsive audit across all 7 viewports on User Details Section D
            foreach (var vp in Viewports)
            {
                await page.SetViewportSizeAsync(vp.Width, vp.Height);
                var vpResp = await page.GotoAsync(memberDetailsUrl, domReady);
                Assert.Equal(200, vpResp?.Status);

                Assert.Equal(1, await page.Locator(".jt-admin-shell").CountAsync());
                Assert.Equal(1, await page.Locator(".jt-admin-topbar").CountAsync());
                Assert.True(await page.Locator("h2:has-text('Role Management & Governance')").IsVisibleAsync(), $"Role management heading not visible at {vp.Width}x{vp.Height}");
                Assert.True(await page.Locator("select[name='TargetRole']").IsVisibleAsync(), $"TargetRole select not visible at {vp.Width}x{vp.Height}");
                Assert.True(await page.Locator("button[type='submit']:has-text('Update Role')").IsVisibleAsync(), $"Update Role button not visible at {vp.Width}x{vp.Height}");

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

            // 4. FLOW A: Promote Member -> Administrator
            await page.SetViewportSizeAsync(1440, 900);
            await page.GotoAsync(memberDetailsUrl, domReady);

            // Select Administrator role and submit
            await page.SelectOptionAsync("select[name='TargetRole']", "Admin");
            await page.ClickAsync("button[type='submit']:has-text('Update Role')");

            // Verify redirect back to user details and updated badges
            await page.WaitForURLAsync("**/admin/users/*");
            var successAlert = page.Locator(".alert-success");
            Assert.True(await successAlert.IsVisibleAsync());
            Assert.Contains("User role updated successfully.", await successAlert.InnerTextAsync());

            // Verify Administrator badge is visible
            Assert.True(await page.Locator(".badge:has-text('Administrator')").First.IsVisibleAsync());

            // 5. FLOW B: Verify Demo/Self Admin demotion protection
            // Navigate to the demo admin user details
            await page.GotoAsync("/admin/users?q=admin@jamesthew.com", domReady);
            var demoDetailsLink = page.Locator("a:has-text('View Details')").First;
            Assert.True(await demoDetailsLink.IsVisibleAsync());
            await demoDetailsLink.ClickAsync();
            await page.WaitForURLAsync("**/admin/users/*");

            // Verify governance lockout alert is rendered
            var governanceAlert = page.Locator(".alert-warning, .alert-info");
            Assert.True(await governanceAlert.First.IsVisibleAsync());
            var alertText = await governanceAlert.First.InnerTextAsync();
            Assert.True(alertText.Contains("Demo Administrator Account") || alertText.Contains("Current Active Session"));

            // Verify Update Role mutation form is NOT available for this protected admin
            Assert.Equal(0, await page.Locator("form[action*='/role'] button[type='submit']").CountAsync());
        }
        finally
        {
            await server.DisposeAsync();
        }
    }
}
