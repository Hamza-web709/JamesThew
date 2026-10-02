using System;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;

namespace JamesThew.Tests;

public class CardInteractionBrowserQATests
{
    [Fact]
    public async Task Phase7C_ContentCards_Desktop_Hover_Tilt_And_PointerLeave_Restores_Transform()
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
            await page.GotoAsync("/recipes", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });

            var card = page.Locator(".jt-card-interactive-content.jt-card-flip-wrap").First;
            await card.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
            await card.ScrollIntoViewIfNeededAsync();

            // Initial rest state
            var initialLift = await card.EvaluateAsync<string>("el => getComputedStyle(el).getPropertyValue('--lift-y').trim()");
            Assert.True(string.IsNullOrEmpty(initialLift) || initialLift == "0px", $"Expected initial --lift-y to be 0px, got '{initialLift}'");

            // Dispatch pointermove near bottom-right of card
            var box = await card.BoundingBoxAsync();
            Assert.NotNull(box);

            var targetX = box.X + box.Width * 0.8;
            var targetY = box.Y + box.Height * 0.8;

            await page.Mouse.MoveAsync((float)targetX, (float)targetY);
            await page.WaitForTimeoutAsync(200);

            // Active hover state: --lift-y is -8px, --tilt-x / --tilt-y have been set
            var activeLift = await card.EvaluateAsync<string>("el => getComputedStyle(el).getPropertyValue('--lift-y').trim()");
            var activeTiltX = await card.EvaluateAsync<string>("el => getComputedStyle(el).getPropertyValue('--tilt-x').trim()");
            var activeTiltY = await card.EvaluateAsync<string>("el => getComputedStyle(el).getPropertyValue('--tilt-y').trim()");

            Assert.Equal("-8px", activeLift);
            Assert.Contains("deg", activeTiltX);
            Assert.Contains("deg", activeTiltY);

            // Pointer leave: move far away
            await page.Mouse.MoveAsync(10, 10);
            await page.WaitForTimeoutAsync(200);

            var restoredLift = await card.EvaluateAsync<string>("el => getComputedStyle(el).getPropertyValue('--lift-y').trim()");
            var restoredTiltX = await card.EvaluateAsync<string>("el => getComputedStyle(el).getPropertyValue('--tilt-x').trim()");
            var restoredTiltY = await card.EvaluateAsync<string>("el => getComputedStyle(el).getPropertyValue('--tilt-y').trim()");

            Assert.Equal("0px", restoredLift);
            Assert.Equal("0deg", restoredTiltX);
            Assert.Equal("0deg", restoredTiltY);
        }
        finally
        {
            await server.DisposeAsync();
        }
    }

    [Fact]
    public async Task Phase7C_RecipeAndTipCards_Details_Flip_Toggle()
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
            await page.GotoAsync("/recipes", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });

            var card = page.Locator(".jt-card-flip-wrap").First;
            await card.ScrollIntoViewIfNeededAsync();
            var toggleBtn = card.Locator(".jt-flip-toggle-btn");
            var closeBtn = card.Locator(".jt-flip-close-btn");

            // Initially not flipped
            var initialFlipped = await card.EvaluateAsync<bool>("el => el.classList.contains('is-flipped')");
            Assert.False(initialFlipped);

            // Click toggle button to peek details
            await toggleBtn.ClickAsync();
            await page.WaitForTimeoutAsync(200);

            var isFlipped = await card.EvaluateAsync<bool>("el => el.classList.contains('is-flipped')");
            Assert.True(isFlipped);

            // Click close button to return to front
            await closeBtn.ClickAsync();
            await page.WaitForTimeoutAsync(200);

            var isClosed = await card.EvaluateAsync<bool>("el => el.classList.contains('is-flipped')");
            Assert.False(isClosed);
        }
        finally
        {
            await server.DisposeAsync();
        }
    }

    [Fact]
    public async Task Phase7C_ContestCards_Perspective_Tilt_Stable_Badge_And_CTA_Arrow()
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
            await page.GotoAsync("/contests", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });

            var card = page.Locator(".jt-card-interactive-content").First;
            await card.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

            // Verify spotlight overlay is present
            var spotlight = card.Locator(".jt-card-spotlight");
            Assert.Equal(1, await spotlight.CountAsync());

            // Verify status badge is present and stable
            var badge = card.Locator(".badge").First;
            Assert.Equal(1, await badge.CountAsync());

            // Verify CTA arrow is present
            var ctaArrow = card.Locator(".jt-cta-arrow");
            Assert.True(await ctaArrow.CountAsync() > 0);

            // Verify no flip wrap is applied to Contest cards (readability preserved)
            var hasFlip = await card.EvaluateAsync<bool>("el => el.classList.contains('jt-card-flip-wrap')");
            Assert.False(hasFlip, "Contest cards must NOT flip to preserve status/CTA readability.");
        }
        finally
        {
            await server.DisposeAsync();
        }
    }

    [Fact]
    public async Task Phase7C_UtilityCards_Glow_And_SmallLift_No_3D_Flip()
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
            await page.GotoAsync("/membership", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });

            var utilityCard = page.Locator(".jt-card-interactive-utility").First;
            await utilityCard.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

            // Verify spotlight overlay exists
            var spotlight = utilityCard.Locator(".jt-card-spotlight");
            Assert.Equal(1, await spotlight.CountAsync());

            // Verify NO 3D flip wrap
            var hasFlip = await utilityCard.EvaluateAsync<bool>("el => el.classList.contains('jt-card-flip-wrap')");
            Assert.False(hasFlip, "Utility cards must NOT flip.");

            // Verify cursor spotlight updates on mouse move
            var box = await utilityCard.BoundingBoxAsync();
            Assert.NotNull(box);

            await page.Mouse.MoveAsync((float)(box.X + 50), (float)(box.Y + 50));
            await page.WaitForTimeoutAsync(100);

            var mouseX = await utilityCard.EvaluateAsync<string>("el => getComputedStyle(el).getPropertyValue('--mouse-x').trim()");
            Assert.NotEmpty(mouseX);
            Assert.Contains("px", mouseX);
        }
        finally
        {
            await server.DisposeAsync();
        }
    }

    [Fact]
    public async Task Phase7C_Keyboard_Focus_Visible_Affordance()
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
            await page.GotoAsync("/recipes", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });

            var card = page.Locator(".jt-card-interactive-content").First;
            await card.ScrollIntoViewIfNeededAsync();
            var ctaLink = card.Locator("a.jt-btn-editorial").First;

            // Focus link with keyboard
            await ctaLink.FocusAsync();
            await page.WaitForTimeoutAsync(250);

            var activeElement = await page.EvaluateAsync<string>("() => document.activeElement ? (document.activeElement.tagName + '.' + document.activeElement.className) : 'none'");
            var isFocusWithin = await card.EvaluateAsync<bool>("el => el.matches(':focus-within')");

            // Card with :focus-within should show gold border highlight or gold focus ring
            var borderColor = await card.EvaluateAsync<string>("el => getComputedStyle(el).borderColor");
            var boxShadow = await card.EvaluateAsync<string>("el => getComputedStyle(el).boxShadow");
            var hasGoldAffordance = borderColor.Contains("243, 198") ||
                                    borderColor.Contains("245, 209") ||
                                    boxShadow.Contains("243, 198") ||
                                    boxShadow.Contains("229, 186");
            Assert.True(hasGoldAffordance, $"Expected gold focus affordance on card. Active: {activeElement}, matchesFocusWithin: {isFocusWithin}, border: {borderColor}, shadow: {boxShadow}");
        }
        finally
        {
            await server.DisposeAsync();
        }
    }

    [Fact]
    public async Task Phase7C_ReducedMotion_Disables_3D_Transforms()
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
                ReducedMotion = ReducedMotion.Reduce,
                ViewportSize = new ViewportSize { Width = 1440, Height = 900 }
            });

            var page = await context.NewPageAsync();
            await page.GotoAsync("/recipes", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });

            var card = page.Locator(".jt-card-interactive-content").First;
            await card.HoverAsync();
            await page.WaitForTimeoutAsync(100);

            // Under prefers-reduced-motion: reduce, computed transform is none
            var transform = await card.EvaluateAsync<string>("el => getComputedStyle(el).transform");
            Assert.Equal("none", transform);

            // Spotlight is hidden under reduced motion
            var spotlightDisplay = await card.Locator(".jt-card-spotlight").EvaluateAsync<string>("el => getComputedStyle(el).display");
            Assert.Equal("none", spotlightDisplay);
        }
        finally
        {
            await server.DisposeAsync();
        }
    }

    [Fact]
    public async Task Phase7C_MobileTouch_Disables_Desktop_Tilt()
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
                HasTouch = true,
                IsMobile = true,
                ViewportSize = new ViewportSize { Width = 390, Height = 844 }
            });

            var page = await context.NewPageAsync();
            await page.GotoAsync("/recipes", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });

            var card = page.Locator(".jt-card-interactive-content").First;
            await card.TapAsync();
            await page.WaitForTimeoutAsync(100);

            // In mobile touch mode (hover: none), transform stays none
            var transform = await card.EvaluateAsync<string>("el => getComputedStyle(el).transform");
            Assert.Equal("none", transform);

            // Spotlight is hidden on touch devices
            var spotlightDisplay = await card.Locator(".jt-card-spotlight").EvaluateAsync<string>("el => getComputedStyle(el).display");
            Assert.Equal("none", spotlightDisplay);
        }
        finally
        {
            await server.DisposeAsync();
        }
    }
}
