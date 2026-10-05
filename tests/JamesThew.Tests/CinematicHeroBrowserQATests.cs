using Microsoft.Playwright;
using System.Text.Json;
using Xunit;

namespace JamesThew.Tests;

public class CinematicHeroBrowserQATests
{
    [Fact]
    public async Task CinematicEditorial_FullBrowserQA_Audit()
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
                new { Name = "Desktop-1440x900", Width = 1440, Height = 900 },
                new { Name = "Laptop-1280x720", Width = 1280, Height = 720 },
                new { Name = "Tablet-768x1024", Width = 768, Height = 1024 },
                new { Name = "Mobile-430x932", Width = 430, Height = 932 },
                new { Name = "Mobile-390x844", Width = 390, Height = 844 }
            };

            foreach (var vp in viewports)
            {
                var consoleErrors = new List<string>();
                var context = await browser.NewContextAsync(new BrowserNewContextOptions
                {
                    IgnoreHTTPSErrors = true,
                    BaseURL = server.ServerAddress,
                    ViewportSize = new ViewportSize { Width = vp.Width, Height = vp.Height }
                });

                var page = await context.NewPageAsync();
                page.PageError += (_, msg) => consoleErrors.Add($"[{vp.Name} Page Error]: {msg}");
                page.Console += (_, msg) =>
                {
                    if (msg.Type == "error" && !msg.Text.Contains("favicon"))
                    {
                        consoleErrors.Add($"[{vp.Name} Console Error]: {msg.Text}");
                    }
                };

                var response = await page.GotoAsync("/", new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded,
                    Timeout = 90000
                });
                Assert.NotNull(response);
                Assert.Equal(200, response.Status);

                // Wait for GSAP / assets to settle
                await page.WaitForTimeoutAsync(600);

                // 1. Verify All 10 Continuous Sections Present
                var s1 = page.Locator("#hero-story");
                var s2 = page.Locator("#section-transformation");
                var s3 = page.Locator("#section-story");
                var s4 = page.Locator("#section-collage");
                var s5 = page.Locator("#section-signature");
                var s6 = page.Locator("#section-horizontal");
                var s7 = page.Locator("#section-masterclass");
                var s8 = page.Locator("#section-contests");
                var s9 = page.Locator("#section-cta");
                var s10 = page.Locator("#section-footer");

                Assert.True(await s1.IsVisibleAsync(), $"{vp.Name}: Section 01 (#hero-story) should be visible");
                Assert.True(await s2.CountAsync() > 0, $"{vp.Name}: Section 02 (#section-transformation) should exist");
                Assert.True(await s3.CountAsync() > 0, $"{vp.Name}: Section 03 (#section-story) should exist");
                Assert.True(await s4.CountAsync() > 0, $"{vp.Name}: Section 04 (#section-collage) should exist");
                Assert.True(await s5.CountAsync() > 0, $"{vp.Name}: Section 05 (#section-signature) should exist");
                Assert.True(await s6.CountAsync() > 0, $"{vp.Name}: Section 06 (#section-horizontal) should exist");
                Assert.True(await s7.CountAsync() > 0, $"{vp.Name}: Section 07 (#section-masterclass) should exist");
                Assert.True(await s8.CountAsync() > 0, $"{vp.Name}: Section 08 (#section-contests) should exist");
                Assert.True(await s9.CountAsync() > 0, $"{vp.Name}: Section 09 (#section-cta) should exist");
                Assert.True(await s10.CountAsync() > 0, $"{vp.Name}: Section 10 (#section-footer) should exist");

                // 2. Verify Single H1 Heading
                var h1 = page.Locator("h1");
                Assert.Equal(1, await h1.CountAsync());
                var h1Text = await h1.InnerTextAsync();
                Assert.Contains("Where Ingredients", h1Text);
                Assert.Contains("Become Craft", h1Text);

                // 3. Verify Key Visual Elements Exist
                var video = page.Locator("#heroCookingVideo");
                var floatTomato = page.Locator("#floatTomato");
                var floatBasil = page.Locator("#floatBasil");
                var transformPlatedDish = page.Locator("#transformPlatedDish");
                var sigDishImg = page.Locator("#sigDishImg");
                var horizontalTrack = page.Locator("#horizontalTrack");
                var ctaVisualBlock = page.Locator("#ctaVisualBlock");

                Assert.True(await video.CountAsync() > 0, $"{vp.Name}: #heroCookingVideo should exist");
                Assert.True(await floatTomato.CountAsync() > 0, $"{vp.Name}: #floatTomato should exist");
                Assert.True(await floatBasil.CountAsync() > 0, $"{vp.Name}: #floatBasil should exist");
                Assert.True(await transformPlatedDish.CountAsync() > 0, $"{vp.Name}: #transformPlatedDish should exist");
                Assert.True(await sigDishImg.CountAsync() > 0, $"{vp.Name}: #sigDishImg should exist");
                Assert.True(await horizontalTrack.CountAsync() > 0, $"{vp.Name}: #horizontalTrack should exist");
                Assert.True(await ctaVisualBlock.CountAsync() > 0, $"{vp.Name}: #ctaVisualBlock should exist");

                // 4. Verify No Horizontal Overflow
                var overflowElements = await page.EvaluateAsync<string>(@"() => {
                    const elements = [];
                    document.querySelectorAll('*').forEach(el => {
                        const rect = el.getBoundingClientRect();
                        if (rect.right > window.innerWidth + 1) {
                            elements.push({ tag: el.tagName, id: el.id, class: el.className, right: rect.right, width: rect.width });
                        }
                    });
                    return JSON.stringify(elements);
                }");
                var hasOverflow = await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > window.innerWidth");
                Assert.False(hasOverflow, $"{vp.Name} has horizontal overflow. Offending elements: {overflowElements}");

                // 5. Test Scroll Progression (on Desktop)
                if (vp.Width >= 992)
                {
                    // Verify initial Hero elements
                    Assert.True(await page.Locator("#heroTopLeft").IsVisibleAsync());

                    // Scroll down to Section 03 (The Story)
                    await s3.ScrollIntoViewIfNeededAsync();
                    await page.WaitForTimeoutAsync(300);
                    Assert.True(await s3.IsVisibleAsync());

                    // Scroll down to Section 06 (Horizontal Rail)
                    await s6.ScrollIntoViewIfNeededAsync();
                    await page.WaitForTimeoutAsync(300);
                    Assert.True(await s6.IsVisibleAsync());

                    // Scroll down to Section 09 (CTA)
                    await s9.ScrollIntoViewIfNeededAsync();
                    await page.WaitForTimeoutAsync(300);
                    Assert.True(await s9.IsVisibleAsync());

                    // Scroll to Footer
                    await s10.ScrollIntoViewIfNeededAsync();
                    await page.WaitForTimeoutAsync(300);
                    Assert.True(await s10.IsVisibleAsync());
                    var watermark = page.Locator(".jt-footer-watermark");
                    Assert.Contains("JAMES THEW", await watermark.InnerTextAsync());
                }

                // 6. Mobile Touch Target Audits
                if (vp.Width < 768)
                {
                    var buttons = page.Locator("#hero-story .jt-btn-editorial");
                    var btnCount = await buttons.CountAsync();
                    for (int i = 0; i < btnCount; i++)
                    {
                        var box = await buttons.Nth(i).BoundingBoxAsync();
                        if (box != null)
                        {
                            Assert.True(box.Height >= 40, $"{vp.Name}: Button height {box.Height} should be >= 40px for touch");
                        }
                    }
                }

                // 7. Zero Console Errors
                Assert.Empty(consoleErrors);

                await context.CloseAsync();
            }

            // =========================================================================
            // PART 2: REDUCED MOTION AUDIT (WCAG 2.2 AA)
            // =========================================================================
            {
                var reducedMotionContext = await browser.NewContextAsync(new BrowserNewContextOptions
                {
                    IgnoreHTTPSErrors = true,
                    BaseURL = server.ServerAddress,
                    ReducedMotion = ReducedMotion.Reduce,
                    ViewportSize = new ViewportSize { Width = 1280, Height = 800 }
                });

                var rmPage = await reducedMotionContext.NewPageAsync();
                await rmPage.GotoAsync("/", new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded,
                    Timeout = 90000
                });
                await rmPage.WaitForTimeoutAsync(300);

                // All major sections must be cleanly rendered
                Assert.True(await rmPage.Locator("#hero-story").IsVisibleAsync());
                Assert.True(await rmPage.Locator("#section-story").IsVisibleAsync());
                Assert.True(await rmPage.Locator("#section-horizontal").IsVisibleAsync());
                Assert.True(await rmPage.Locator("#section-footer").IsVisibleAsync());

                // No overflow under reduced motion
                var hasOverflowRm = await rmPage.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > window.innerWidth");
                Assert.False(hasOverflowRm, "Reduced motion view has horizontal overflow.");

                await reducedMotionContext.CloseAsync();
            }
        }
        finally
        {
            await server.DisposeAsync();
        }
    }

    [Fact]
    public async Task ContinuousScroll_BlankViewport_Guard_Audit()
    {
        var server = new BrowserTestServer();
        await server.InitializeAsync();

        try
        {
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                IgnoreHTTPSErrors = true,
                BaseURL = server.ServerAddress,
                ViewportSize = new ViewportSize { Width = 1440, Height = 900 }
            });

            var page = await context.NewPageAsync();
            await page.GotoAsync("/", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 90000 });
            await page.WaitForTimeoutAsync(600);

            // 1. Verify Floating Glass Capsule Header
            var header = page.Locator("#jtHeader");
            Assert.True(await header.IsVisibleAsync(), "Floating glass capsule header (#jtHeader) must be visible");
            var headerBox = await header.BoundingBoxAsync();
            Assert.NotNull(headerBox);
            Assert.True(headerBox.Width <= 1180, $"Header width {headerBox.Width} should be <= 1180px");
            Assert.True(headerBox.Height >= 50 && headerBox.Height <= 80, $"Header height {headerBox.Height} should be 60-68px");

            // Verify initial theme is dark
            var initialTheme = await header.GetAttributeAsync("data-theme");
            Assert.Equal("dark", initialTheme);

            // Verify Sign In is a compact capsule button inside the header
            var signInBtn = page.Locator(".jt-capsule-signin-btn");
            Assert.True(await signInBtn.IsVisibleAsync(), "Compact Sign In button should be visible in header");

            // 2. Automated Blank-Viewport QA Sampling across Pinned / Animated Sections
            var sectionAnchors = new Dictionary<string, string[]>
            {
                ["#hero-story"] = new[] { "#heroCookingVideo", "#heroTopLeft", "#heroMidRight", "#heroBottomRight", "#floatTomato", "#floatBasil" },
                ["#section-transformation"] = new[] { "#convergeGarlic", "#thermalCore", "#kineticBurst", "#plateWrapper", "#transformPlatedDish", "#transformDishTag", "#transformLeftText" },
                ["#section-story"] = new[] { ".jt-story-card", ".jt-story-headline", ".jt-milestone-col", ".jt-milestone-stat" },
                ["#section-collage"] = new[] { "#mosaicCardA", "#mosaicCardB", "#mosaicCardC", "#mosaicCardFocal", "#collageDarkCurtain", ".jt-collage-headline" },
                ["#section-signature"] = new[] { "#sigDishCenter", "#sigTextTop", "#sigTextBottom", "#sigSpecs", "#sigHalo" },
                ["#section-horizontal"] = new[] { ".jt-rail-card" },
                ["#section-masterclass"] = new[] { "#techCard1", "#techCard2", "#techCard3", ".jt-technique-headline" },
                ["#section-contests"] = new[] { ".jt-contest-card", ".jt-contests-headline" },
                ["#section-cta"] = new[] { ".jt-cta-massive-title", "#ctaVisualBlock", ".jt-btn-editorial" }
            };

            foreach (var (sectionId, anchors) in sectionAnchors)
            {
                var section = page.Locator(sectionId);
                Assert.True(await section.CountAsync() > 0, $"Section {sectionId} must exist");

                // Get section vertical bounds
                var bounds = await page.EvaluateAsync<double[]>(@"id => {
                    const el = document.querySelector(id);
                    if (!el) return [0, 0];
                    const rect = el.getBoundingClientRect();
                    const scrollY = window.scrollY;
                    return [rect.top + scrollY, rect.bottom + scrollY];
                }", sectionId);

                double top = bounds[0];
                double bottom = bounds[1];
                double height = bottom - top;

                // Sample 8 scroll positions across this section (progress 0.0 to 1.0)
                int sampleCount = 8;
                for (int i = 0; i <= sampleCount; i++)
                {
                    double progress = (double)i / sampleCount;
                    double targetScrollY = top + (height * progress) - (progress == 0 ? 0 : 200);
                    if (targetScrollY < 0) targetScrollY = 0;

                    await page.EvaluateAsync("y => window.scrollTo(0, y)", targetScrollY);
                    await page.WaitForTimeoutAsync(120);

                    // Check that at least ONE visual anchor in this section has opacity > 0.2 AND bounding box in viewport
                    var hasVisibleAnchor = await page.EvaluateAsync<bool>(@"args => {
                        const [sectionId, anchorSelectors] = args;
                        const sec = document.querySelector(sectionId);
                        if (!sec) return false;

                        const vpHeight = window.innerHeight;
                        const vpWidth = window.innerWidth;

                        for (const sel of anchorSelectors) {
                            const els = sec.querySelectorAll(sel);
                            for (const el of els) {
                                const style = window.getComputedStyle(el);
                                const opacity = parseFloat(style.opacity || '1');
                                const visibility = style.visibility;
                                const display = style.display;

                                if (display === 'none' || visibility === 'hidden' || opacity < 0.2) {
                                    continue;
                                }

                                const rect = el.getBoundingClientRect();
                                const inViewport = (rect.bottom > 0 && rect.top < vpHeight && rect.right > 0 && rect.left < vpWidth);
                                if (inViewport && rect.width > 10 && rect.height > 10) {
                                    return true;
                                }
                            }
                        }
                        return false;
                    }", new object[] { sectionId, anchors });

                    Assert.True(hasVisibleAnchor, $"BLANK VIEWPORT DETECTED: At scroll point {targetScrollY:F0}px (progress {progress:P0}) in section {sectionId}, all visual anchors were hidden or offscreen!");
                }
            }

            // 3. Verify Image Diversity (No Repeated Beef Wellington Dominance)
            var s2DishSrc = await page.Locator("#transformPlatedDish").GetAttributeAsync("src");
            Assert.NotNull(s2DishSrc);
            Assert.DoesNotContain("beef-wellington", s2DishSrc.ToLowerInvariant());
            Assert.Contains("chicken", s2DishSrc.ToLowerInvariant());

            var s6Card1Src = await page.Locator("#section-horizontal .jt-rail-card img").First.GetAttributeAsync("src");
            Assert.NotNull(s6Card1Src);
            Assert.DoesNotContain("beef-wellington", s6Card1Src.ToLowerInvariant());
            Assert.Contains("lobster", s6Card1Src.ToLowerInvariant());

            // Wellington only featured as focal card in S04 and signature dish in S05
            var s4FocalSrc = await page.Locator("#focalDishImg").GetAttributeAsync("src");
            Assert.Contains("beef-wellington", s4FocalSrc?.ToLowerInvariant() ?? "");

            var s5DishSrc = await page.Locator("#sigDishImg").GetAttributeAsync("src");
            Assert.Contains("beef-wellington", s5DishSrc?.ToLowerInvariant() ?? "");

            await context.CloseAsync();
        }
        finally
        {
            await server.DisposeAsync();
        }
    }

    [Fact]
    public async Task Cornerstone_HorizontalRail_ScrollGeometry_And_SingleOccurrence_Audit()
    {
        var server = new BrowserTestServer();
        await server.InitializeAsync();

        try
        {
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });

            var viewports = new[]
            {
                new { Name = "Desktop-1440x900", Width = 1440, Height = 900, IsDesktop = true },
                new { Name = "Desktop-1280x720", Width = 1280, Height = 720, IsDesktop = true },
                new { Name = "Tablet-768x1024", Width = 768, Height = 1024, IsDesktop = false },
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
                await page.GotoAsync("/", new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded,
                    Timeout = 90000
                });
                await page.WaitForFunctionAsync(@"() =>
                    window.ScrollTrigger &&
                    typeof window.ScrollTrigger.getAll === 'function' &&
                    window.ScrollTrigger.getAll().some(st => st.trigger && st.trigger.id === 'section-horizontal')");
                await page.EvaluateAsync("() => window.ScrollTrigger.refresh(true)");
                await page.WaitForTimeoutAsync(300);

                if (vp.IsDesktop)
                {
                    // 1. Verify exact DOM occurrences: exactly ONE Four Cornerstone section heading
                    var headlineCount = await page.Locator(".jt-horizontal-headline").CountAsync();
                    Assert.Equal(1, headlineCount);

                    // 2. Measure section scroll bounds around #section-horizontal
                    var bounds = await page.EvaluateAsync<double[]>(@"() => {
                        const sec = document.querySelector('#section-horizontal');
                        if (!sec) return [0, 0];
                        const spacer = sec.closest('.pin-spacer') || sec;
                        const rect = spacer.getBoundingClientRect();
                        const scrollY = window.scrollY;
                        return [rect.top + scrollY, rect.bottom + scrollY];
                    }");

                    double sectionStart = bounds[0];
                    double sectionEnd = bounds[1];
                    Assert.True(sectionEnd > sectionStart, $"{vp.Name}: #section-horizontal bounds must be positive");

                    // Sample from 300px before section (Section 05) to 300px after section (Section 07)
                    double startY = Math.Max(0, sectionStart - 300);
                    double endY = sectionEnd + 300;
                    double step = 80;

                    var techBounds = await page.EvaluateAsync<double[]>(@"() => {
                        const el = document.querySelector('#section-masterclass');
                        if (!el) return [0, 0];
                        const rect = el.getBoundingClientRect();
                        const scrollY = window.scrollY;
                        return [rect.top + scrollY, rect.bottom + scrollY];
                    }");

                    var samplePoints = new List<(double Y, string Label)>();
                    for (double y = startY; y <= endY; y += step)
                    {
                        samplePoints.Add((y, $"fine-{y:F0}"));
                    }

                    double railHeight = sectionEnd - sectionStart;
                    samplePoints.Add((sectionStart, "rail-entry"));
                    samplePoints.Add((sectionStart + railHeight * 0.25, "rail-25"));
                    samplePoints.Add((sectionStart + railHeight * 0.50, "rail-50"));
                    samplePoints.Add((sectionStart + railHeight * 0.75, "rail-75"));
                    samplePoints.Add((sectionEnd, "rail-end"));
                    samplePoints.Add((sectionEnd + 1, "first-frame-after-unpin"));
                    samplePoints.Add((techBounds[0], "next-section-entry"));

                    samplePoints = samplePoints
                        .Where(point => point.Y >= 0)
                        .GroupBy(point => Math.Round(point.Y))
                        .Select(group => group.First())
                        .OrderBy(point => point.Y)
                        .ToList();

                    async Task<RailSampleAudit> AuditRailSampleAsync(double y, string label, int waitMs)
                    {
                        await page.EvaluateAsync(@"async y => {
                            const scroller = document.scrollingElement || document.documentElement;
                            const maxY = Math.max(0, scroller.scrollHeight - window.innerHeight);
                            const targetY = Math.max(0, Math.min(y, maxY));

                            for (let attempt = 0; attempt < 4; attempt++) {
                                window.scrollTo({ top: targetY, left: 0, behavior: 'instant' });
                                await new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)));
                                if (Math.abs(window.scrollY - targetY) <= 6) {
                                    return;
                                }
                            }
                        }", y);
                        await page.WaitForTimeoutAsync(waitMs);

                        var auditJson = await page.EvaluateAsync<string>(@"args => {
                            const [label, targetY] = args;
                            const vpHeight = window.innerHeight;
                            const vpWidth = window.innerWidth;

                            function rectObj(el) {
                                if (!el) return null;
                                const rect = el.getBoundingClientRect();
                                return {
                                    top: Math.round(rect.top * 100) / 100,
                                    bottom: Math.round(rect.bottom * 100) / 100,
                                    left: Math.round(rect.left * 100) / 100,
                                    right: Math.round(rect.right * 100) / 100,
                                    width: Math.round(rect.width * 100) / 100,
                                    height: Math.round(rect.height * 100) / 100
                                };
                            }

                            function isVisible(el, minSize = 20) {
                                if (!el) return false;
                                const style = window.getComputedStyle(el);
                                if (style.display === 'none' || style.visibility === 'hidden' || parseFloat(style.opacity || '1') < 0.2) return false;
                                const rect = el.getBoundingClientRect();
                                if (rect.width < minSize || rect.height < minSize) return false;
                                return rect.bottom > 0 && rect.top < vpHeight && rect.right > 0 && rect.left < vpWidth;
                            }

                            const section = document.querySelector('#section-horizontal');
                            const spacer = section ? section.closest('.pin-spacer') : null;
                            const track = document.querySelector('#horizontalTrack');
                            const masterclass = document.querySelector('#section-masterclass');
                            const visibleRailCards = Array.from(document.querySelectorAll('.jt-rail-card')).filter(el => isVisible(el, 80)).length;
                            const visibleHeadlineCount = Array.from(document.querySelectorAll('.jt-horizontal-headline')).filter(el => isVisible(el, 20)).length;
                            const s5Anchors = Array.from(document.querySelectorAll('#sigDishCenter, #sigDishImg, #sigSpecs, #sigTextTop')).some(el => isVisible(el, 40));
                            const railCardAnchors = visibleRailCards > 0;
                            const s7Anchors = Array.from(document.querySelectorAll('#techCard1, #techCard2, #techCard3, .jt-technique-headline')).some(el => isVisible(el, 40));

                            let clippingAudit = 'ok';
                            if (section) {
                                const secRect = section.getBoundingClientRect();
                                const isPinned = Math.abs(secRect.top) <= 5 && secRect.bottom >= vpHeight - 5;
                                if (isPinned) {
                                    const cards = Array.from(document.querySelectorAll('.jt-rail-card'));
                                    for (const card of cards) {
                                        const rect = card.getBoundingClientRect();
                                        if (rect.right > 50 && rect.left < window.innerWidth - 50) {
                                            if (rect.top < -10 || rect.bottom > vpHeight + 10) {
                                                clippingAudit = `Card vertically clipped while pinned: top=${rect.top}, bottom=${rect.bottom}, vpHeight=${vpHeight}`;
                                                break;
                                            }
                                            const title = card.querySelector('.jt-rail-title');
                                            const link = card.querySelector('.jt-rail-link');
                                            if (!title || !link) {
                                                clippingAudit = 'Missing title or link in rail card';
                                                break;
                                            }
                                        }
                                    }
                                }
                            }

                            let railTrigger = null;
                            if (window.ScrollTrigger && typeof window.ScrollTrigger.getAll === 'function') {
                                railTrigger = window.ScrollTrigger.getAll()
                                    .filter(st => st.trigger === section || st.pin === section)
                                    .map(st => ({
                                        start: Math.round(st.start * 100) / 100,
                                        end: Math.round(st.end * 100) / 100,
                                        progress: Math.round(st.progress * 10000) / 10000,
                                        isActive: !!st.isActive
                                    }))[0] || null;
                            }

                            return JSON.stringify({
                                label,
                                targetY: Math.round(targetY * 100) / 100,
                                scrollY: Math.round(window.scrollY * 100) / 100,
                                sectionRect: rectObj(section),
                                pinSpacerRect: rectObj(spacer),
                                masterclassRect: rectObj(masterclass),
                                horizontalTrackTransform: track ? window.getComputedStyle(track).transform : null,
                                visibleRailCardCount: visibleRailCards,
                                visibleHeadlineCount,
                                scrollTrigger: railTrigger,
                                trackScrollWidth: track ? track.scrollWidth : null,
                                viewport: { width: vpWidth, height: vpHeight },
                                hasMeaningfulAnchor: s5Anchors || railCardAnchors || s7Anchors,
                                anchorGroups: { signature: s5Anchors, rail: railCardAnchors, masterclass: s7Anchors },
                                clippingAudit
                            });
                        }", new object[] { label, y });

                        using var document = JsonDocument.Parse(auditJson);
                        var root = document.RootElement;
                        return new RailSampleAudit(
                            root.GetProperty("hasMeaningfulAnchor").GetBoolean(),
                            root.GetProperty("visibleHeadlineCount").GetInt32(),
                            root.GetProperty("clippingAudit").GetString() ?? "ok",
                            auditJson);
                    }

                    // Forward scroll test
                    foreach (var (y, label) in samplePoints)
                    {
                        var audit = await AuditRailSampleAsync(y, label, 90);

                        Assert.True(audit.VisibleHeadlineCount <= 1,
                            $"{vp.Name}: At {label} scroll Y={y:F0}px, expected at most 1 visible Cornerstone headline, but found {audit.VisibleHeadlineCount}. Diagnostics: {audit.Diagnostics}");

                        Assert.True(audit.HasMeaningfulAnchor,
                            $"BLANK VIEWPORT / DEAD ZONE DETECTED at {label} scroll Y={y:F0}px in {vp.Name}. Diagnostics: {audit.Diagnostics}");

                        Assert.Equal("ok", audit.ClippingAudit);
                    }

                    // Reverse scroll test: scroll back up from endY to startY
                    foreach (var (y, label) in samplePoints.AsEnumerable().Reverse())
                    {
                        var audit = await AuditRailSampleAsync(y, $"reverse-{label}", 70);

                        Assert.True(audit.HasMeaningfulAnchor,
                            $"Reverse scroll: blank viewport at {label} scroll Y={y:F0}px in {vp.Name}. Diagnostics: {audit.Diagnostics}");
                    }
                }
                else
                {
                    // Tablet & Mobile: static flow verification
                    var cards = page.Locator("#section-horizontal .jt-rail-card");
                    Assert.Equal(4, await cards.CountAsync());

                    // Scroll to Section 06
                    var s6 = page.Locator("#section-horizontal");
                    await s6.ScrollIntoViewIfNeededAsync();
                    await page.WaitForTimeoutAsync(200);

                    // Verify cards are stacked and visible without pin trapping
                    Assert.True(await cards.First.IsVisibleAsync(), $"{vp.Name}: First rail card must be visible on mobile");
                    Assert.True(await cards.Last.IsVisibleAsync(), $"{vp.Name}: Last rail card must be visible on mobile");

                    // Exactly one headline
                    var headlineCount = await page.Locator(".jt-horizontal-headline").CountAsync();
                    Assert.Equal(1, headlineCount);
                }

                await context.CloseAsync();
            }
        }
        finally
        {
            await server.DisposeAsync();
        }
    }

    private sealed record RailSampleAudit(
        bool HasMeaningfulAnchor,
        int VisibleHeadlineCount,
        string ClippingAudit,
        string Diagnostics);
}
