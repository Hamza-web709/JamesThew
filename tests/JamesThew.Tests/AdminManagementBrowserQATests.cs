using Microsoft.Playwright;
using Xunit;

namespace JamesThew.Tests;

public class AdminManagementBrowserQATests
{
    private static byte[] ValidJpegBytes => ReadAssetBytes("wwwroot", "images", "recipes", "classic-roast-chicken.jpg");

    private static byte[] ReadAssetBytes(params string[] relativeParts)
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(new[] { dir, "JamesThew" }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate))
            {
                return File.ReadAllBytes(candidate);
            }
            dir = Path.GetDirectoryName(dir);
        }
        throw new FileNotFoundException("Could not locate test image asset.", Path.Combine(relativeParts));
    }

    [Fact]
    public async Task Phase7I_AdminManagement_MediaAndActionLayout_Audit()
    {
        var server = new BrowserTestServer();
        await server.InitializeAsync();

        var validName = $"phase7i-valid-{server.RunId}.jpg";
        var invalidName = $"phase7i-invalid-{server.RunId}.jpg";
        await File.WriteAllBytesAsync(Path.Combine(server.TestUploadDir, validName), ValidJpegBytes);
        await File.WriteAllTextAsync(Path.Combine(server.TestUploadDir, invalidName), new string('x', 256));

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

            await page.GotoAsync("/account/login");
            await page.FillAsync("input[name='Email']", "admin@jamesthew.com");
            await page.FillAsync("input[name='Password']", "Admin@Pass1234!");
            await page.ClickAsync("form[action*='/account/login'] button[type='submit']");
            await page.WaitForURLAsync("**/admin");

            foreach (var viewport in new[]
            {
                new ViewportSize { Width = 1440, Height = 900 },
                new ViewportSize { Width = 390, Height = 844 }
            })
            {
                await page.SetViewportSizeAsync(viewport.Width, viewport.Height);

                await page.GotoAsync("/admin/media");
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                await page.WaitForFunctionAsync(@"() => {
                    const img = document.querySelector('img[src*=""phase7i-valid""]');
                    return img && img.complete && img.naturalWidth > 0;
                }");
                await page.WaitForFunctionAsync(@"() => document.querySelector('.jt-admin-media-preview.is-unavailable')");

                var mediaIssues = await page.EvaluateAsync<string[]>(@"() => {
                    const viewport = document.documentElement.clientWidth;
                    const issues = [];
                    if (document.documentElement.scrollWidth > viewport + 1) issues.push('horizontal overflow');
                    for (const img of document.querySelectorAll('.jt-admin-media-img')) {
                        if (!img.hidden && img.complete && img.naturalWidth === 0) issues.push('native broken image exposed');
                    }
                    for (const action of document.querySelectorAll('.jt-admin-media-actions .jt-admin-table-action')) {
                        const rect = action.getBoundingClientRect();
                        if (rect.height < 40) issues.push('small media action');
                        if (rect.left < -1 || rect.right > viewport + 1) issues.push('clipped media action');
                    }
                    const fallback = document.querySelector('.jt-admin-media-preview.is-unavailable .jt-media-preview-fallback');
                    if (!fallback || getComputedStyle(fallback).display === 'none') issues.push('missing unavailable fallback');
                    return issues;
                }");
                Assert.Empty(mediaIssues);

                foreach (var route in new[] { "/admin/content", "/admin/contests" })
                {
                    await page.GotoAsync(route);
                    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                    var actionIssues = await page.EvaluateAsync<string[]>(@"() => {
                        const viewport = document.documentElement.clientWidth;
                        const issues = [];
                        if (document.documentElement.scrollWidth > viewport + 1) issues.push('horizontal overflow');
                        const actions = Array.from(document.querySelectorAll('.jt-admin-table-action'));
                        if (actions.length === 0) issues.push('missing actions');
                        for (const action of actions) {
                            const rect = action.getBoundingClientRect();
                            if (rect.width > 0 && rect.height < 40) issues.push('small action target');
                            if (rect.width > 0 && (rect.left < -1 || rect.right > viewport + 1)) issues.push(`clipped action ${action.textContent.trim()} ${Math.round(rect.left)}-${Math.round(rect.right)} vw ${viewport}`);
                        }
                        for (const cluster of document.querySelectorAll('.jt-admin-action-cluster')) {
                            const buttons = Array.from(cluster.querySelectorAll('.jt-admin-table-action'))
                                .filter(button => button.getBoundingClientRect().width > 0);
                            for (let i = 0; i < buttons.length; i++) {
                                const a = buttons[i].getBoundingClientRect();
                                for (let j = i + 1; j < buttons.length; j++) {
                                    const b = buttons[j].getBoundingClientRect();
                                    const overlaps = a.left < b.right && a.right > b.left && a.top < b.bottom && a.bottom > b.top;
                                    if (overlaps) issues.push('overlapping actions');
                                }
                            }
                        }
                        return issues;
                    }");
                    Assert.True(actionIssues.Length == 0, $"{route} {viewport.Width}x{viewport.Height}: {string.Join("; ", actionIssues)}");
                }
            }
        }
        finally
        {
            await server.DisposeAsync();
        }
    }
}
