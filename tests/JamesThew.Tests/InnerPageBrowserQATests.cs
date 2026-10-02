using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;

namespace JamesThew.Tests;

public class InnerPageBrowserQATests
{
    [Fact]
    public async Task Phase7B_InnerPages_Image_Integrity_Audit()
    {
        var server = new BrowserTestServer();
        await server.InitializeAsync();

        try
        {
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });

            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                IgnoreHTTPSErrors = true,
                BaseURL = server.ServerAddress,
                ViewportSize = new ViewportSize { Width = 1440, Height = 900 }
            });

            var page = await context.NewPageAsync();
            var failedImageRequests = new List<string>();

            page.Response += (_, res) =>
            {
                var request = res.Request;
                if (request.ResourceType == "image" && res.Status >= 400)
                {
                    failedImageRequests.Add($"{res.Status}: {res.Url}");
                }
            };

            var testRoutes = new[]
            {
                "/recipes",
                "/recipes?visibility=Free",
                "/recipes?visibility=MembersOnly",
                "/recipes/jamess-classic-roast-herb-chicken",
                "/recipes/jamess-masterclass-beef-wellington",
                "/tips",
                "/tips?visibility=Free",
                "/tips?visibility=MembersOnly",
                "/tips/mastering-chefs-knife-grip-and-precision-cuts",
                "/tips/the-science-of-pan-searing-and-the-maillard-reaction",
                "/tips/masterclass-french-sauce-emulsions-and-pan-deglazing"
            };

            foreach (var route in testRoutes)
            {
                await page.GotoAsync(route);
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

                // Verify every image element has completed and has non-zero natural dimensions
                var imageAudit = await page.EvaluateAsync<ImageCheckResult[]>(@"() => {
                    const imgs = Array.from(document.querySelectorAll('img'));
                    return imgs.map(img => ({
                        src: img.src,
                        alt: img.alt,
                        complete: img.complete,
                        naturalWidth: img.naturalWidth,
                        naturalHeight: img.naturalHeight,
                        isDisplayed: img.offsetParent !== null || window.getComputedStyle(img).display !== 'none'
                    }));
                }");

                foreach (var img in imageAudit)
                {
                    // Ignore empty or data URI if any
                    if (string.IsNullOrEmpty(img.Src) || img.Src.StartsWith("data:")) continue;

                    Assert.True(img.Complete, $"Image {img.Src} on {route} should be complete.");
                    Assert.True(img.NaturalWidth > 0, $"Image {img.Src} on {route} has 0 naturalWidth (broken image).");
                    Assert.True(img.NaturalHeight > 0, $"Image {img.Src} on {route} has 0 naturalHeight (broken image).");
                }
            }

            Assert.Empty(failedImageRequests);
        }
        finally
        {
            await server.DisposeAsync();
        }
    }

    [Fact]
    public async Task Phase7B_InnerPages_Footer_And_Stacking_Audit()
    {
        var server = new BrowserTestServer();
        await server.InitializeAsync();

        try
        {
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });

            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                IgnoreHTTPSErrors = true,
                BaseURL = server.ServerAddress,
                ViewportSize = new ViewportSize { Width = 1440, Height = 900 }
            });

            var page = await context.NewPageAsync();

            // 1. Audit public/guest routes in guest context (unauthenticated)
            var guestRoutes = new[]
            {
                "/recipes",
                "/recipes/jamess-classic-roast-herb-chicken",
                "/tips",
                "/tips/mastering-chefs-knife-grip-and-precision-cuts",
                "/contests",
                "/contests/autumn-heritage-stew-showdown",
                "/announcements",
                "/feedback",
                "/faq",
                "/membership",
                "/account/login",
                "/account/register",
                "/account/accessdenied",
                "/home/privacy"
            };

            foreach (var route in guestRoutes)
            {
                await page.GotoAsync(route);
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

                var footer = page.Locator("footer.jt-footer");
                var count = await footer.CountAsync();
                Assert.True(count == 1, $"Expected exactly 1 footer on {route}, found {count}");

                var isVisible = await footer.IsVisibleAsync();
                Assert.True(isVisible, $"Footer must be visible on {route}");

                // Check stacking and positioning
                var footerStyles = await page.EvaluateAsync<FooterStyleResult>(@"() => {
                    const footer = document.querySelector('footer.jt-footer');
                    if (!footer) return null;
                    const style = window.getComputedStyle(footer);
                    const rect = footer.getBoundingClientRect();
                    return {
                        position: style.position,
                        zIndex: parseInt(style.zIndex, 10) || 0,
                        backgroundColor: style.backgroundColor,
                        height: rect.height,
                        top: rect.top
                    };
                }");

                Assert.NotNull(footerStyles);
                Assert.True(footerStyles.Position == "relative" || footerStyles.ZIndex >= 10,
                    $"Footer on {route} must have explicit stacking (position: {footerStyles.Position}, zIndex: {footerStyles.ZIndex})");
                Assert.True(footerStyles.Height > 100, $"Footer on {route} should have substantive height, was {footerStyles.Height}px");
            }

            // 2. Audit authenticated route (/account/status)
            await page.GotoAsync("/account/login");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.FillAsync("input[name='Email']", "admin@jamesthew.com");
            await page.FillAsync("input[name='Password']", "Admin@Pass1234!");
            await page.ClickAsync("button[type='submit']");
            await page.WaitForURLAsync("**/admin");

            await page.GotoAsync("/account/status", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });

            var statusFooter = page.Locator("footer.jt-footer");
            Assert.True(await statusFooter.CountAsync() == 1, "Expected exactly 1 footer on /account/status");
            Assert.True(await statusFooter.IsVisibleAsync(), "Footer must be visible on /account/status");
        }
        finally
        {
            await server.DisposeAsync();
        }
    }

    [Fact]
    public async Task Phase7B_InnerPages_Button_Styling_And_Contrast_Audit()
    {
        var server = new BrowserTestServer();
        await server.InitializeAsync();

        try
        {
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });

            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                IgnoreHTTPSErrors = true,
                BaseURL = server.ServerAddress,
                ViewportSize = new ViewportSize { Width = 1440, Height = 900 }
            });

            var page = await context.NewPageAsync();

            // 1. Audit Unauthenticated Feedback Page CTAs
            await page.GotoAsync("/feedback");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var signInBtn = page.Locator("a.jt-btn-editorial.jt-btn-gold:has-text('Sign In to Submit Feedback')");
            Assert.True(await signInBtn.IsVisibleAsync(), "Feedback page must render primary gold CTA for guest.");

            var registerBtn = page.Locator("a.jt-btn-editorial.jt-btn-outline-light:has-text('Register New Account')");
            Assert.True(await registerBtn.IsVisibleAsync(), "Feedback page must render secondary outline CTA for guest.");

            var btnMetrics = await page.EvaluateAsync<ButtonMetrics>(@"() => {
                const gold = document.querySelector('a.jt-btn-editorial.jt-btn-gold');
                const outline = document.querySelector('a.jt-btn-editorial.jt-btn-outline-light');
                const goldRect = gold ? gold.getBoundingClientRect() : { height: 0 };
                const outlineRect = outline ? outline.getBoundingClientRect() : { height: 0 };
                const goldStyle = gold ? window.getComputedStyle(gold) : null;
                return {
                    goldHeight: goldRect.height,
                    outlineHeight: outlineRect.height,
                    goldBorderRadius: goldStyle ? goldStyle.borderRadius : '',
                    goldColor: goldStyle ? goldStyle.color : ''
                };
            }");

            Assert.True(btnMetrics.GoldHeight >= 40, $"Feedback gold CTA height should be >= 40px, was {btnMetrics.GoldHeight}");
            Assert.True(btnMetrics.OutlineHeight >= 40, $"Feedback outline CTA height should be >= 40px, was {btnMetrics.OutlineHeight}");

            // 2. Audit Login Primary Submit Button
            await page.GotoAsync("/account/login");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var loginSubmit = page.Locator("button.jt-btn-editorial.jt-btn-gold[type='submit']");
            Assert.True(await loginSubmit.IsVisibleAsync(), "Login page must have styled gold submit button.");

            var loginMetrics = await page.EvaluateAsync<ButtonMetrics>(@"() => {
                const btn = document.querySelector('button.jt-btn-editorial.jt-btn-gold[type=\'submit\']');
                const rect = btn.getBoundingClientRect();
                const style = window.getComputedStyle(btn);
                return {
                    goldHeight: rect.height,
                    outlineHeight: 0,
                    goldBorderRadius: style.borderRadius,
                    goldColor: style.color
                };
            }");
            Assert.True(loginMetrics.GoldHeight >= 44, $"Login submit button height should be >= 44px, was {loginMetrics.GoldHeight}");

            // 3. Audit Register Primary Submit Button
            await page.GotoAsync("/account/register");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var registerSubmit = page.Locator("button.jt-btn-editorial.jt-btn-gold[type='submit']");
            Assert.True(await registerSubmit.IsVisibleAsync(), "Register page must have styled gold submit button.");

            // 4. Audit FAQ Text Contrast
            await page.GotoAsync("/faq");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var faqTextContrast = await page.EvaluateAsync<bool>(@"() => {
                const answer = document.querySelector('.jt-faq-answer');
                if (!answer) return false;
                const style = window.getComputedStyle(answer);
                // Ensure it is not dark brown #5C554B (rgb(92, 85, 75))
                return !style.color.includes('92, 85, 75');
            }");
            Assert.True(faqTextContrast, "FAQ answer text must not use low-contrast dark brown color.");
        }
        finally
        {
            await server.DisposeAsync();
        }
    }

    [Fact]
    public async Task Phase7B_InnerPages_Responsive_Audit()
    {
        var server = new BrowserTestServer();
        await server.InitializeAsync();

        try
        {
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });

            var viewports = new[]
            {
                new ViewportSize { Width = 1440, Height = 900 },
                new ViewportSize { Width = 1280, Height = 720 },
                new ViewportSize { Width = 768, Height = 1024 },
                new ViewportSize { Width = 430, Height = 932 },
                new ViewportSize { Width = 390, Height = 844 }
            };

            foreach (var vp in viewports)
            {
                var context = await browser.NewContextAsync(new BrowserNewContextOptions
                {
                    IgnoreHTTPSErrors = true,
                    BaseURL = server.ServerAddress,
                    ViewportSize = vp
                });

                var page = await context.NewPageAsync();

                var testPages = new[] { "/recipes", "/tips", "/faq", "/membership", "/account/login" };

                foreach (var p in testPages)
                {
                    await page.GotoAsync(p);
                    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

                    // Check horizontal overflow
                    var hasOverflow = await page.EvaluateAsync<bool>(@"() => {
                        return document.documentElement.scrollWidth > window.innerWidth + 2;
                    }");
                    Assert.False(hasOverflow, $"Horizontal overflow detected at {p} on {vp.Width}x{vp.Height}");

                    // Check footer is visible
                    var footerVisible = await page.Locator("footer.jt-footer").IsVisibleAsync();
                    Assert.True(footerVisible, $"Footer must be visible at {p} on {vp.Width}x{vp.Height}");
                }

                await context.CloseAsync();
            }
        }
        finally
        {
            await server.DisposeAsync();
        }
    }

    private class ImageCheckResult
    {
        public string Src { get; set; } = "";
        public string Alt { get; set; } = "";
        public bool Complete { get; set; }
        public int NaturalWidth { get; set; }
        public int NaturalHeight { get; set; }
        public bool IsDisplayed { get; set; }
    }

    private class FooterStyleResult
    {
        public string Position { get; set; } = "";
        public int ZIndex { get; set; }
        public string BackgroundColor { get; set; } = "";
        public double Height { get; set; }
        public double Top { get; set; }
    }

    private class ButtonMetrics
    {
        public double GoldHeight { get; set; }
        public double OutlineHeight { get; set; }
        public string GoldBorderRadius { get; set; } = "";
        public string GoldColor { get; set; } = "";
    }
}
