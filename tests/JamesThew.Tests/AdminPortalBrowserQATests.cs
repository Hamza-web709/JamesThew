using Microsoft.Playwright;
using Xunit;

namespace JamesThew.Tests;

public class AdminPortalBrowserQATests
{
    [Fact]
    public async Task Phase7H_AdminPortal_RoleNavigation_And_Responsive_Audit()
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
            var domReadyNavigation = new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 90000
            };

            await page.GotoAsync("/account/login", domReadyNavigation);
            await page.FillAsync("input[name='Email']", "admin@jamesthew.com");
            await page.FillAsync("input[name='Password']", "Admin@Pass1234!");
            await page.ClickAsync("form[action*='/account/login'] button[type='submit']");
            await page.WaitForURLAsync("**/admin");

            Assert.Equal(1, await page.Locator(".jt-admin-shell").CountAsync());
            Assert.Equal(0, await page.Locator("header.jt-header").CountAsync());
            Assert.Equal(0, await page.Locator("footer.jt-footer").CountAsync());
            Assert.True(await page.Locator("a.jt-admin-public-link:has-text('View Public Site')").IsVisibleAsync());
            Assert.True(await page.Locator(".jt-admin-nav-link:has-text('Editorial Content')").IsVisibleAsync());
            Assert.True(await page.Locator(".jt-admin-nav-link:has-text('Media Library')").IsVisibleAsync());
            Assert.True(await page.Locator(".jt-admin-nav-link:has-text('Membership')").IsVisibleAsync());
            Assert.True(await page.Locator(".jt-admin-nav-link:has-text('Feedback')").IsVisibleAsync());
            Assert.True(await page.Locator(".jt-admin-nav-link:has-text('Content Intake')").IsVisibleAsync());
            Assert.True(await page.Locator(".jt-admin-nav-link:has-text('Contest Management')").IsVisibleAsync());
            Assert.Equal(0, await page.Locator(".jt-admin-nav-link:has-text('Announcements')").CountAsync());

            var adminRoutes = new[]
            {
                "/admin",
                "/admin/content",
                "/admin/media",
                "/admin/subscriptions",
                "/admin/feedback",
                "/admin/contributions",
                "/admin/contests",
                "/admin/content/recipes/new",
                "/admin/content/tips/new",
                "/admin/contests/new"
            };

            var viewports = new[]
            {
                new ViewportSize { Width = 1440, Height = 900 },
                new ViewportSize { Width = 1280, Height = 720 },
                new ViewportSize { Width = 1024, Height = 768 },
                new ViewportSize { Width = 768, Height = 1024 },
                new ViewportSize { Width = 430, Height = 932 },
                new ViewportSize { Width = 390, Height = 844 }
            };

            foreach (var viewport in viewports)
            {
                await page.SetViewportSizeAsync(viewport.Width, viewport.Height);
                foreach (var route in adminRoutes)
                {
                    var response = await page.GotoAsync(route, domReadyNavigation);
                    Assert.Equal(200, response?.Status);
                    Assert.Equal(1, await page.Locator(".jt-admin-shell").CountAsync());
                    Assert.Equal(0, await page.Locator("header.jt-header").CountAsync());
                    Assert.Equal(0, await page.Locator("footer.jt-footer").CountAsync());
                    Assert.True(await page.Locator(".jt-admin-footer").IsVisibleAsync(), $"Admin footer hidden on {route} at {viewport.Width}x{viewport.Height}");

                    var issues = await page.EvaluateAsync<string[]>(@"() => {
                        const viewport = document.documentElement.clientWidth;
                        const issues = [];
                        if (document.documentElement.scrollWidth > viewport + 1) issues.push('horizontal overflow');
                        for (const target of document.querySelectorAll('.jt-admin-topbar a, .jt-admin-topbar button, .jt-admin-nav-link')) {
                            const sidebar = target.closest('.jt-admin-sidebar');
                            if (sidebar && viewport <= 1180 && !sidebar.classList.contains('show')) continue;
                            const rect = target.getBoundingClientRect();
                            if (rect.width > 0 && rect.height < 43) issues.push('small target');
                            if (rect.width > 0 && (rect.left < -1 || rect.right > viewport + 1)) issues.push('clipped action');
                        }
                        return issues;
                    }");
                    Assert.Empty(issues);

                    if (viewport.Width <= 1180)
                    {
                        await page.ClickAsync("#adminMenuToggle");
                        await page.WaitForTimeoutAsync(260);
                        var drawerIssues = await page.EvaluateAsync<string[]>(@"() => {
                            const viewport = document.documentElement.clientWidth;
                            const issues = [];
                            for (const target of document.querySelectorAll('.jt-admin-sidebar.show .jt-admin-nav-link')) {
                                const rect = target.getBoundingClientRect();
                                if (rect.height < 43) issues.push('small drawer target');
                                if (rect.left < -1 || rect.right > viewport + 1) issues.push('drawer clipping');
                            }
                            return issues;
                        }");
                        Assert.Empty(drawerIssues);
                        await page.Keyboard.PressAsync("Escape");
                        Assert.False(await page.Locator("#adminSidebar").EvaluateAsync<bool>("el => el.classList.contains('show')"));
                    }
                }
            }

            await page.SetViewportSizeAsync(1440, 900);
            await page.GotoAsync("/", domReadyNavigation);
            Assert.Equal(1, await page.Locator("header.jt-header").CountAsync());
            Assert.True(await page.Locator(".jt-nav-auth a:has-text('Admin Console')").IsVisibleAsync());
            Assert.Equal(0, await page.Locator(".jt-nav-auth a:has-text('Contribute')").CountAsync());
            Assert.Equal(0, await page.Locator(".jt-nav-auth a:has-text('My Entries')").CountAsync());

            foreach (var viewport in viewports)
            {
                await page.SetViewportSizeAsync(viewport.Width, viewport.Height);
                await page.GotoAsync("/", domReadyNavigation);
                if (viewport.Width <= 1100)
                {
                    await page.ClickAsync("#navbarToggleBtn");
                    Assert.True(await page.Locator(".jt-nav-auth a:has-text('Admin Console')").IsVisibleAsync());
                    Assert.True(await page.Locator(".jt-capsule-logout-btn").IsVisibleAsync());
                    await page.Keyboard.PressAsync("Escape");
                }

                var publicHeaderIssues = await page.EvaluateAsync<string[]>(@"() => {
                    const viewport = document.documentElement.clientWidth;
                    const issues = [];
                    if (document.documentElement.scrollWidth > viewport + 1) issues.push('horizontal overflow');
                    const header = document.querySelector('.jt-header');
                    if (!header) issues.push('missing header');
                    const visibleActions = Array.from(document.querySelectorAll('.jt-nav-auth a, .jt-nav-auth button'))
                        .filter(target => target.getBoundingClientRect().width > 0);
                    for (const target of visibleActions) {
                        const rect = target.getBoundingClientRect();
                        if (rect.left < -1 || rect.right > viewport + 1) issues.push('public header clipping');
                        if (rect.height < 36) issues.push('small public action');
                    }
                    return issues;
                }");
                Assert.Empty(publicHeaderIssues);
            }

            await page.SetViewportSizeAsync(1440, 900);
            Assert.Equal(200, (await page.GotoAsync("/contributions", domReadyNavigation))?.Status);

            var publicViewIssues = await page.EvaluateAsync<string[]>(@"() => {
                const viewport = document.documentElement.clientWidth;
                const issues = [];
                if (document.documentElement.scrollWidth > viewport + 1) issues.push('horizontal overflow');
                const logout = document.querySelector('.jt-capsule-logout-btn');
                const admin = Array.from(document.querySelectorAll('.jt-nav-auth a')).find(a => a.textContent.includes('Admin Console'));
                for (const target of [logout, admin]) {
                    if (!target) continue;
                    const rect = target.getBoundingClientRect();
                    if (rect.width > 0 && (rect.left < -1 || rect.right > viewport + 1)) issues.push('public nav clipping');
                }
                return issues;
            }");
            Assert.Empty(publicViewIssues);

            await page.GotoAsync("/feedback", domReadyNavigation);
            var ratingValues = await page.Locator("select[name='Form.Rating'] option").EvaluateAllAsync<string[]>(
                "options => options.map(option => option.value || '')");
            Assert.Equal(new[] { "", "5", "4", "3", "2", "1" }, ratingValues);

            var categoryValues = await page.Locator("select[name='Form.Category'] option").EvaluateAllAsync<string[]>(
                "options => options.map(option => option.value || '')");
            Assert.Contains("General Culinary Feedback", categoryValues);
            Assert.Contains("Recipe Technique Question", categoryValues);
            Assert.Contains("Masterclass Suggestion", categoryValues);
            Assert.Contains("Portal Feature Suggestion", categoryValues);
            Assert.Contains("Ingredients & Sourcing Inquiry", categoryValues);

            await page.ClickAsync("form[action*='/account/logout'] button[type='submit']");
            await page.WaitForURLAsync("**/");
            Assert.True(await page.Locator(".jt-nav-auth a:has-text('Register')").IsVisibleAsync());
            Assert.True(await page.Locator(".jt-nav-auth a:has-text('Sign In')").IsVisibleAsync());

            await TestAuth.LoginBrowserMemberAsync(page, server.MemberEmail, "Member@Pass1234!");
            await page.WaitForURLAsync("**/account/status");
            Assert.True(await page.Locator(".jt-nav-auth a:has-text('Contribute')").IsVisibleAsync());
            Assert.True(await page.Locator(".jt-nav-auth a:has-text('My Entries')").IsVisibleAsync());
            Assert.Equal(0, await page.Locator(".jt-nav-auth a:has-text('Admin Console')").CountAsync());
        }
        finally
        {
            await server.DisposeAsync();
        }
    }
}
