using Microsoft.Playwright;
using Xunit;

namespace JamesThew.Tests;

public class MemberPageBrowserQATests
{
    [Fact]
    public async Task Phase7D_MemberPages_Responsive_And_Form_Content_Audit()
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

            await TestAuth.LoginBrowserMemberAsync(page, server.MemberEmail, "Member@Pass1234!");
            await page.WaitForURLAsync("**/account/status*");

            await page.GotoAsync("/contributions/recipe/new");
            await page.FillAsync("input[name='Title']", "Responsive QA Recipe");
            await page.FillAsync("textarea[name='Summary']", "A browser QA recipe with complete culinary details.");
            await page.FillAsync("input[name='Servings']", "4");
            await page.FillAsync("input[name='PrepMinutes']", "15");
            await page.FillAsync("input[name='CookMinutes']", "25");
            await page.FillAsync("textarea[name='IngredientsText']", "200g flour\n100ml water");
            await page.FillAsync("textarea[name='StepsText']", "Mix the ingredients thoroughly.\nCook until done.");
            await page.ClickAsync("#btnSubmitRecipe");
            await page.WaitForURLAsync("**/contributions");
            var recipeEdit = await page.Locator("a[id^='btnEditRecipe-']").First.GetAttributeAsync("href");
            Assert.False(string.IsNullOrEmpty(recipeEdit));

            await page.GotoAsync("/contributions/tip/new");
            await page.FillAsync("input[name='Title']", "Responsive QA Tip");
            await page.FillAsync("textarea[name='Summary']", "A practical browser QA cooking tip summary.");
            await page.FillAsync("textarea[name='Body']", "Keep the pan at medium heat and stir steadily for an even result.");
            await page.ClickAsync("#btnSubmitTip");
            await page.WaitForURLAsync("**/contributions");
            var tipEdit = await page.Locator("a[id^='btnEditTip-']").First.GetAttributeAsync("href");
            Assert.False(string.IsNullOrEmpty(tipEdit));

            await page.GotoAsync(recipeEdit);
            Assert.Contains("200g flour\n100ml water", await page.Locator("textarea[name='IngredientsText']").InputValueAsync());
            Assert.Contains("Mix the ingredients thoroughly.\nCook until done.", await page.Locator("textarea[name='StepsText']").InputValueAsync());

            var contestSlug = "autumn-heritage-stew-showdown";
            await page.GotoAsync($"/contests/{contestSlug}/entry");
            await page.FillAsync("input[name='Title']", "Responsive QA Contest Entry");
            await page.FillAsync("textarea[name='Summary']", "A slow cooked dish prepared for responsive QA review.");
            await page.FillAsync("input[name='Servings']", "4");
            await page.FillAsync("input[name='PrepMinutes']", "20");
            await page.FillAsync("input[name='CookMinutes']", "90");
            await page.FillAsync("textarea[name='IngredientsText']", "500g beef\n2 carrots");
            await page.FillAsync("textarea[name='StepsText']", "Brown the beef thoroughly.\nSimmer with carrots until tender.");
            await page.ClickAsync("#btnSubmitContestEntry");
            await page.WaitForURLAsync("**/contests/my-entries*");

            var routes = new[]
            {
                "/contributions", "/contributions/recipe/new", recipeEdit!,
                "/contributions/tip/new", tipEdit!,
                $"/contests/{contestSlug}/entry", "/contests/my-entries",
                $"/contests/{contestSlug}/my-entry"
            };
            var sizes = new[] { (1440, 900), (1280, 720), (768, 1024), (430, 932), (390, 844) };
            foreach (var (width, height) in sizes)
            {
                await page.SetViewportSizeAsync(width, height);
                foreach (var route in routes)
                {
                    var response = await page.GotoAsync(route);
                    Assert.Equal(200, response?.Status);
                    Assert.Equal(1, await page.Locator(".jt-inner-shell.jt-member-page").CountAsync());
                    Assert.Equal(1, await page.Locator("footer.jt-footer").CountAsync());
                    var issues = await page.EvaluateAsync<string[]>(@"() => {
                        const viewport = document.documentElement.clientWidth;
                        const issues = [];
                        if (document.documentElement.scrollWidth > viewport + 1) issues.push('horizontal overflow');
                        for (const control of document.querySelectorAll('.jt-member-page input:not([type=hidden]), .jt-member-page textarea')) {
                            const rect = control.getBoundingClientRect();
                            if (rect.width > 0 && (rect.left < -1 || rect.right > viewport + 1)) issues.push('field overflow');
                        }
                        for (const button of document.querySelectorAll('.jt-member-page .jt-btn-editorial')) {
                            const rect = button.getBoundingClientRect();
                            if (rect.width > 0 && rect.height < 43) issues.push('small button');
                        }
                        return issues;
                    }");
                    Assert.Empty(issues);
                    await page.Locator("footer.jt-footer").ScrollIntoViewIfNeededAsync();
                    Assert.True(await page.Locator("footer.jt-footer").IsVisibleAsync(), $"Footer hidden on {route} at {width}x{height}");
                }
            }
        }
        finally
        {
            await server.DisposeAsync();
        }
    }
}
