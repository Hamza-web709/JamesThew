using Microsoft.Playwright;
using Xunit;

namespace JamesThew.Tests;

public class Phase8ABrowserQATests
{
    [Fact]
    public async Task Phase8A_Otp_Checkout_And_Payment_Pages_Are_Responsive()
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
            var email = $"phase8a_{server.RunId}@jamesthew.test";
            var password = "Phase8A@Pass1234!";

            await page.GotoAsync("/account/register");
            await page.FillAsync("input[name='DisplayName']", "Phase 8A Browser");
            await page.FillAsync("input[name='Email']", email);
            await page.FillAsync("input[name='Password']", password);
            await page.FillAsync("input[name='ConfirmPassword']", password);
            await page.ClickAsync("form[action*='/account/register'] button[type='submit']");
            await page.WaitForURLAsync("**/account/otp/email*");
            await AssertResponsive(page, ".jt-otp-box", 6);
            await TestAuth.CompleteBrowserEmailOtpAsync(page, email);
            await page.WaitForURLAsync("**/account/status*");

            await page.SetViewportSizeAsync(1440, 900);
            await page.ClickAsync("form[action*='/account/logout'] button[type='submit']");
            await page.WaitForURLAsync("**/");
            await page.GotoAsync("/account/login");
            await page.FillAsync("input[name='Email']", email);
            await page.FillAsync("input[name='Password']", password);
            await page.ClickAsync("form[action*='/account/login'] button[type='submit']");
            await page.WaitForURLAsync("**/account/otp/email*");
            await AssertResponsive(page, ".jt-otp-box", 6);
            await TestAuth.CompleteBrowserEmailOtpAsync(page, email);
            await page.WaitForURLAsync("**/account/status*");

            await page.GotoAsync("/membership");
            await AssertResponsive(page, ".jt-glass-panel", minCount: 1);

            await page.GotoAsync("/membership/checkout?plan=Monthly");
            await AssertResponsive(page, ".jt-demo-card-form", minCount: 1);
            await page.FillAsync("input[name='CardholderName']", "Phase 8A Browser");
            await page.FillAsync("input[name='CardNumber']", "4242 4242 4242 4242");
            await page.FillAsync("input[name='Expiry']", "12/30");
            await page.FillAsync("input[name='Cvv']", "123");
            await page.ClickAsync("form[action*='/membership/checkout'] button[type='submit']");
            await page.WaitForURLAsync("**/membership/payment-otp*");
            await AssertResponsive(page, ".jt-otp-box", 4);
            await TestAuth.CompleteBrowserDemoPaymentOtpAsync(page);
            await page.WaitForURLAsync("**/membership*");
            Assert.Contains("Active Premium Access", await page.ContentAsync());
        }
        finally
        {
            await server.DisposeAsync();
        }
    }

    private static async Task AssertResponsive(IPage page, string selector, int minCount)
    {
        var sizes = new[] { (1440, 900), (1280, 720), (768, 1024), (430, 932), (390, 844) };
        foreach (var (width, height) in sizes)
        {
            await page.SetViewportSizeAsync(width, height);
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
            Assert.True(await page.Locator(selector).CountAsync() >= minCount, $"{selector} missing at {width}x{height}.");
            var issues = await page.EvaluateAsync<string[]>(@"() => {
                const viewport = document.documentElement.clientWidth;
                const issues = [];
                if (document.documentElement.scrollWidth > viewport + 1) issues.push('horizontal overflow');
                for (const el of document.querySelectorAll('input:not([type=hidden]), button, a.jt-btn-editorial')) {
                    const rect = el.getBoundingClientRect();
                    if (rect.width > 0 && (rect.left < -1 || rect.right > viewport + 1)) issues.push('clipped control');
                }
                return issues;
            }");
            Assert.Empty(issues);
        }
    }
}
