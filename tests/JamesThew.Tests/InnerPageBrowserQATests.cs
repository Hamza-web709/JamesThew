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

    [Fact]
    public async Task PublicPages_CardGrid_TitleStyling_And_HeaderOverflow_Audit()
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

            // 1. Title Styling and Archive Grid Audit on /recipes and /tips at 1440x900
            {
                var context = await browser.NewContextAsync(new BrowserNewContextOptions
                {
                    IgnoreHTTPSErrors = true,
                    BaseURL = server.ServerAddress,
                    ViewportSize = new ViewportSize { Width = 1440, Height = 900 }
                });
                var page = await context.NewPageAsync();

                foreach (var path in new[] { "/recipes", "/tips" })
                {
                    await page.GotoAsync(path, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
                    await page.WaitForTimeoutAsync(400);

                    // A. Title Styling: must not be browser-default blue link (#0000ee / rgb(13, 110, 253)) and must have no underline at rest
                    var titlesAudit = await page.EvaluateAsync<string>(@"() => {
                        const links = Array.from(document.querySelectorAll('.jt-deck-card-title a, .jt-card-title-link'));
                        const bad = [];
                        for (const a of links) {
                            const cs = window.getComputedStyle(a);
                            const col = cs.color;
                            const decor = cs.textDecorationLine || cs.textDecoration;
                            if (col.includes('13, 110, 253') || col.includes('0, 0, 238') || decor.includes('underline')) {
                                bad.push({ text: a.innerText.trim(), color: col, decoration: decor });
                            }
                        }
                        return JSON.stringify({ totalChecked: links.length, bad });
                    }");

                    using var doc = System.Text.Json.JsonDocument.Parse(titlesAudit);
                    var root = doc.RootElement;
                    Assert.True(root.GetProperty("totalChecked").GetInt32() > 0, $"{path}: expected title links to be present");
                    Assert.Equal(0, root.GetProperty("bad").GetArrayLength());

                    // B. Archive Grid Audit: must use CSS Grid with 3 columns on desktop and no overlapping cards
                    var gridAudit = await page.EvaluateAsync<string>(@"() => {
                        const grid = document.querySelector('.jt-archive-grid');
                        if (!grid) return JSON.stringify({ hasGrid: false });
                        const gridCs = window.getComputedStyle(grid);
                        const cards = Array.from(grid.querySelectorAll('.jt-archive-card'));
                        const rects = cards.map(c => {
                            const r = c.getBoundingClientRect();
                            return { left: Math.round(r.left), right: Math.round(r.right), top: Math.round(r.top), bottom: Math.round(r.bottom), width: Math.round(r.width), height: Math.round(r.height) };
                        });
                        const colTemplate = gridCs.gridTemplateColumns;
                        const colCount = colTemplate.split(' ').length;
                        return JSON.stringify({
                            hasGrid: true,
                            display: gridCs.display,
                            colCount,
                            cardsCount: cards.length,
                            rects
                        });
                    }");

                    using var gridDoc = System.Text.Json.JsonDocument.Parse(gridAudit);
                    var gRoot = gridDoc.RootElement;
                    Assert.True(gRoot.GetProperty("hasGrid").GetBoolean(), $"{path}: .jt-archive-grid must exist");
                    Assert.Equal("grid", gRoot.GetProperty("display").GetString());
                    Assert.True(gRoot.GetProperty("colCount").GetInt32() >= 3, $"{path}: desktop grid should have 3 columns");

                    // Verify cards in same row have different left offsets and do not overlap
                    var rects = gRoot.GetProperty("rects");
                    if (rects.GetArrayLength() >= 2)
                    {
                        var c0 = rects[0];
                        var c1 = rects[1];
                        Assert.True(c1.GetProperty("left").GetInt32() > c0.GetProperty("left").GetInt32(),
                            $"{path}: Second card must be placed horizontally to the right of first card in grid");
                        Assert.True(c1.GetProperty("left").GetInt32() >= c0.GetProperty("right").GetInt32() - 5,
                            $"{path}: Grid cards must not overlap horizontally");
                    }
                }
                await context.CloseAsync();
            }

            // 2. Responsive Header Overflow Audit with Authenticated Member Navigation
            {
                var viewports = new[]
                {
                    new { Name = "Desktop-1440x900", Width = 1440, Height = 900, IsDesktop = true },
                    new { Name = "Desktop-1366x768", Width = 1366, Height = 768, IsDesktop = true },
                    new { Name = "Desktop-1280x720", Width = 1280, Height = 720, IsDesktop = true },
                    new { Name = "Tablet-1024x768", Width = 1024, Height = 768, IsDesktop = false },
                    new { Name = "Tablet-768x1024", Width = 768, Height = 1024, IsDesktop = false },
                    new { Name = "Mobile-430x932", Width = 430, Height = 932, IsDesktop = false },
                    new { Name = "Mobile-390x844", Width = 390, Height = 844, IsDesktop = false }
                };

                foreach (var vp in viewports)
                {
                    var context = await browser.NewContextAsync(new BrowserNewContextOptions
                    {
                        IgnoreHTTPSErrors = true,
                        BaseURL = server.ServerAddress,
                        ViewportSize = new ViewportSize { Width = vp.Width, Height = vp.Height }
                    });
                    var page = await context.NewPageAsync();

                    await page.GotoAsync("/recipes", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });

                    // Inject full authenticated member navigation links into header to test member overflow
                    await page.EvaluateAsync(@"() => {
                        const auth = document.querySelector('.jt-nav-auth');
                        if (auth) {
                            auth.innerHTML = `
                                <a class=""jt-nav-link jt-nav-membership"" href=""/Membership"">Membership</a>
                                <a class=""jt-nav-link"" href=""/Contributions"">Contribute</a>
                                <a class=""jt-nav-link"" href=""/Contests/MyEntries"">My Entries</a>
                                <a class=""jt-nav-link"" href=""/Account/Status"">Account</a>
                                <form method=""post"" class=""d-inline"" action=""/Account/Logout"">
                                    <button class=""jt-btn jt-btn-outline jt-btn-sm jt-capsule-logout-btn"" type=""submit"">Logout</button>
                                </form>
                            `;
                        }
                    }");
                    await page.WaitForTimeoutAsync(100);

                    var headerAudit = await page.EvaluateAsync<string>(@"() => {
                        const header = document.querySelector('#jtHeader');
                        const logoutBtn = document.querySelector('.jt-capsule-logout-btn');
                        const hRect = header ? header.getBoundingClientRect() : null;
                        const lRect = logoutBtn ? logoutBtn.getBoundingClientRect() : null;
                        const isVisible = lRect && (lRect.width > 0 && lRect.height > 0);
                        return JSON.stringify({
                            headerWidth: hRect ? Math.round(hRect.width) : 0,
                            headerRight: hRect ? Math.round(hRect.right) : 0,
                            logoutRight: lRect ? Math.round(lRect.right) : 0,
                            isLogoutVisible: isVisible,
                            scrollWidth: document.documentElement.scrollWidth,
                            windowWidth: window.innerWidth
                        });
                    }");

                    using var hDoc = System.Text.Json.JsonDocument.Parse(headerAudit);
                    var hRoot = hDoc.RootElement;
                    var scrollWidth = hRoot.GetProperty("scrollWidth").GetInt32();
                    var windowWidth = hRoot.GetProperty("windowWidth").GetInt32();

                    // No horizontal page overflow
                    Assert.True(scrollWidth <= windowWidth + 2,
                        $"{vp.Name}: document scrollWidth ({scrollWidth}) must not exceed viewport width ({windowWidth})");

                    if (vp.IsDesktop)
                    {
                        var headerRight = hRoot.GetProperty("headerRight").GetInt32();
                        var logoutRight = hRoot.GetProperty("logoutRight").GetInt32();
                        Assert.True(hRoot.GetProperty("isLogoutVisible").GetBoolean(),
                            $"{vp.Name}: Logout button must be visible in desktop header");
                        Assert.True(logoutRight <= headerRight,
                            $"{vp.Name}: Logout button (right={logoutRight}) must remain completely inside capsule header (right={headerRight})");
                    }

                    await context.CloseAsync();
                }
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
