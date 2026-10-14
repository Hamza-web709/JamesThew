using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;

namespace JamesThew.Tests;

public class PasswordResetBrowserQATests
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
    public async Task Password_reset_flow_is_responsive_across_all_viewports()
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
            var email = $"qa_reset_{server.RunId}@jamesthew.test";
            var initialPassword = "Initial@Pass1234!";
            var newPassword = "Updated@Pass9876!";

            // Register a user first
            await page.GotoAsync("/account/register");
            await page.FillAsync("input[name='DisplayName']", "Reset QA User");
            await page.FillAsync("input[name='Email']", email);
            await page.FillAsync("input[name='Password']", initialPassword);
            await page.FillAsync("input[name='ConfirmPassword']", initialPassword);
            var regTime = DateTime.UtcNow;
            await page.ClickAsync("form[action*='/account/register'] button[type='submit']");
            await page.WaitForURLAsync("**/account/otp/email*");
            await TestAuth.CompleteBrowserEmailOtpAsync(page, email, regTime);
            await page.WaitForURLAsync("**/account/status*");

            // Log out
            await page.ClickAsync("form[action*='/account/logout'] button[type='submit']");
            await page.WaitForURLAsync("**/");

            // 1. LOGIN PAGE across viewports: verify "Forgot password?" is visible
            await page.GotoAsync("/account/login");
            foreach (var (width, height) in Viewports)
            {
                await page.SetViewportSizeAsync(width, height);
                await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
                await AssertNoHorizontalOverflowAndClippedControls(page, $"{width}x{height} Login");
                var forgotLink = page.Locator("a[href*='/account/forgot-password']");
                Assert.True(await forgotLink.IsVisibleAsync(), $"Forgot password link not visible at {width}x{height}");
            }

            // 2. FORGOT PASSWORD REQUEST PAGE across viewports
            await page.ClickAsync("a[href*='/account/forgot-password']");
            await page.WaitForURLAsync("**/account/forgot-password*");
            foreach (var (width, height) in Viewports)
            {
                await page.SetViewportSizeAsync(width, height);
                await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
                await AssertNoHorizontalOverflowAndClippedControls(page, $"{width}x{height} ForgotPassword");
                Assert.True(await page.Locator("input[name='Email']").IsVisibleAsync());
                Assert.True(await page.Locator("button[type='submit']").IsVisibleAsync());
            }

            // Submit forgot password request
            await page.SetViewportSizeAsync(1440, 900);
            await page.FillAsync("input[name='Email']", email);
            var resetTime = DateTime.UtcNow;
            await page.ClickAsync("form[action*='/account/forgot-password'] button[type='submit']");
            await page.WaitForURLAsync("**/account/forgot-password/verify*");

            // 3. RESET OTP VERIFY PAGE across viewports: verify 6 OTP boxes and resend cooldown
            foreach (var (width, height) in Viewports)
            {
                await page.SetViewportSizeAsync(width, height);
                await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
                await AssertNoHorizontalOverflowAndClippedControls(page, $"{width}x{height} ResetOtp");
                var boxes = page.Locator(".jt-otp-box");
                Assert.Equal(6, await boxes.CountAsync());
                for (var i = 0; i < 6; i++)
                {
                    Assert.True(await boxes.Nth(i).IsVisibleAsync(), $"OTP box {i + 1} not visible at {width}x{height}");
                }
            }

            // Enter 6-digit OTP
            var resetCode = TestAuth.LatestOtpFor(email, null, resetTime);
            var otpBoxes = page.Locator(".jt-otp-box");
            for (var i = 0; i < resetCode.Length; i++)
            {
                await otpBoxes.Nth(i).FillAsync(resetCode[i].ToString());
            }
            await Task.WhenAll(
                page.WaitForURLAsync("**/account/reset-password?*"),
                page.ClickAsync("form[action*='/account/forgot-password/verify'] button[type='submit']")
            );

            // 4. NEW PASSWORD PAGE across viewports
            foreach (var (width, height) in Viewports)
            {
                await page.SetViewportSizeAsync(width, height);
                await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
                await AssertNoHorizontalOverflowAndClippedControls(page, $"{width}x{height} NewPassword");
                Assert.True(await page.Locator("input[name='NewPassword']").IsVisibleAsync());
                Assert.True(await page.Locator("input[name='ConfirmPassword']").IsVisibleAsync());
                Assert.True(await page.Locator("button[type='submit']").IsVisibleAsync());
            }

            // Submit new password
            await page.SetViewportSizeAsync(1440, 900);
            await page.FillAsync("input[name='NewPassword']", newPassword);
            await page.FillAsync("input[name='ConfirmPassword']", newPassword);
            await Task.WhenAll(
                page.WaitForURLAsync("**/account/reset-password/confirmation*"),
                page.ClickAsync("form[action*='/account/reset-password'] button[type='submit']")
            );

            // 5. RESET SUCCESS PAGE across viewports
            foreach (var (width, height) in Viewports)
            {
                await page.SetViewportSizeAsync(width, height);
                await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
                await AssertNoHorizontalOverflowAndClippedControls(page, $"{width}x{height} Confirmation");
                var content = await page.ContentAsync();
                Assert.Contains("Password successfully changed.", content);
                var signInBtn = page.Locator(".jt-form-card a[href*='/account/login']");
                Assert.True(await signInBtn.IsVisibleAsync(), $"Sign In button not visible at {width}x{height}");
            }
        }
        finally
        {
            await server.DisposeAsync();
        }
    }

    private static async Task AssertNoHorizontalOverflowAndClippedControls(IPage page, string contextLabel)
    {
        var issues = await page.EvaluateAsync<string[]>(@"() => {
            const viewport = document.documentElement.clientWidth;
            const issues = [];
            if (document.documentElement.scrollWidth > viewport + 1) {
                issues.push('horizontal overflow: scrollWidth=' + document.documentElement.scrollWidth + ' clientWidth=' + viewport);
            }
            for (const el of document.querySelectorAll('input:not([type=hidden]), button, a.jt-btn-editorial, .jt-otp-box')) {
                const rect = el.getBoundingClientRect();
                if (rect.width > 0 && (rect.left < -1 || rect.right > viewport + 1)) {
                    issues.push('clipped control: ' + (el.name || el.className || el.tagName) + ' left=' + rect.left + ' right=' + rect.right);
                }
            }
            return issues;
        }");
        Assert.True(issues.Length == 0, $"Layout issues detected on {contextLabel}: {string.Join("; ", issues)}");
    }
}
