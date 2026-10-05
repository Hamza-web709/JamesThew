using Microsoft.Playwright;
using Xunit;

namespace JamesThew.Tests;

public class ContestBrowserE2ETests
{
    [Fact]
    public async Task Phase5A_Automated_Browser_QA_Contest_Lifecycle()
    {
        var server = new BrowserTestServer();
        await server.InitializeAsync();

        try
        {
            var runId = Guid.NewGuid().ToString("N")[..6].ToLowerInvariant();
            var recipeContestTitle = "Autumn Artisanal Stew Contest " + runId;
            var expectedRecipeSlug = "autumn-artisanal-stew-contest-" + runId;
            var updatedRecipeTitle = "Autumn Artisanal Stew Championship " + runId;
            var draftContestTitle = "Draft Sourdough Fermentation Tip " + runId;
            var draftContestSlug = "draft-sourdough-fermentation-tip-" + runId;

            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });

            // 1. Admin Browser Context
            var adminContext = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                IgnoreHTTPSErrors = true,
                BaseURL = server.ServerAddress
            });
            var adminPage = await adminContext.NewPageAsync();

            // 2. Guest Browser Context
            var guestContext = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                IgnoreHTTPSErrors = true,
                BaseURL = server.ServerAddress
            });
            var guestPage = await guestContext.NewPageAsync();

            // -------------------------------------------------------------
            // Step 1: Admin Login
            // -------------------------------------------------------------
            await adminPage.GotoAsync("/account/login");
            await adminPage.FillAsync("input[name='Email']", "admin@jamesthew.com");
            await adminPage.FillAsync("input[name='Password']", "Admin@Pass1234!");
            await adminPage.ClickAsync("form[action*='/account/login'] button[type='submit']");
            await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var adminNav = await adminPage.Locator("a:has-text('Admin')").CountAsync();
            Assert.True(adminNav > 0, "Admin link must be visible in navbar after login.");

            // -------------------------------------------------------------
            // Step 2: Open /admin/contests
            // -------------------------------------------------------------
            await adminPage.GotoAsync("/admin/contests");
            await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
            var h1 = await adminPage.Locator("h1").InnerTextAsync();
            Assert.Contains("Contests Management", h1);

            // -------------------------------------------------------------
            // Step 3: Admin creates a published recipe contest
            // -------------------------------------------------------------
            await adminPage.GotoAsync("/admin/contests/new");
            await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            await adminPage.FillAsync("input[name='Title']", recipeContestTitle);
            await adminPage.FillAsync("textarea[name='Summary']", "Craft an extraordinary slow-simmered stew celebrating root vegetables and rich broth.");
            await adminPage.FillAsync("textarea[name='DescriptionAndRules']", "Chef James Thew invites all registered members to submit their tested recipes.\n1. Must include ingredient quantities.\n2. Must include oven or stovetop temperatures.\n3. Clear step directions required.");
            await adminPage.FillAsync("input[name='PrizeDescription']", "Masterclass Bronze Trophy and Editorial Feature");

            // Dates: Open now (yesterday to 20 days later)
            var now = DateTime.UtcNow;
            await adminPage.FillAsync("input[name='OpensAt']", now.AddDays(-1).ToString("yyyy-MM-ddTHH:mm"));
            await adminPage.FillAsync("input[name='ClosesAt']", now.AddDays(20).ToString("yyyy-MM-ddTHH:mm"));

            await adminPage.CheckAsync("input#typeRecipe");
            await adminPage.CheckAsync("input#statusPublished");

            await Task.WhenAll(
                adminPage.WaitForURLAsync("**/admin/contests*"),
                adminPage.ClickAsync("form[action*='/admin/contests'] button[type='submit']")
            );
            await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Confirm flash message / presence on admin table
            var adminTableHtml = await adminPage.ContentAsync();
            Assert.Contains(recipeContestTitle, adminTableHtml);

            // -------------------------------------------------------------
            // Step 4: Public Guest browsing at /contests
            // -------------------------------------------------------------
            await guestPage.GotoAsync("/contests");
            await guestPage.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
            await guestPage.Locator($"text={recipeContestTitle}").First.WaitForAsync();

            var guestListingHtml = await guestPage.ContentAsync();
            Assert.Contains(recipeContestTitle, guestListingHtml);
            Assert.Contains("Open Now", guestListingHtml);

            // -------------------------------------------------------------
            // Step 5: Guest opens detail view /contests/{slug}
            // -------------------------------------------------------------
            await guestPage.GotoAsync($"/contests/{expectedRecipeSlug}");
            await guestPage.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
            await guestPage.Locator($"text={recipeContestTitle}").First.WaitForAsync();

            var detailHtml = await guestPage.ContentAsync();
            Assert.Contains(recipeContestTitle, detailHtml);
            Assert.Contains("Slow-simmered stew celebrating root vegetables", detailHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Masterclass Bronze Trophy", detailHtml);
            // Verify member participation notice for guests
            Assert.Contains("Log In to Prepare Entry", detailHtml);
            Assert.Contains("Create Free Member Account", detailHtml);

            // -------------------------------------------------------------
            // Step 6: Admin creates a draft tip contest
            // -------------------------------------------------------------
            await adminPage.GotoAsync("/admin/contests/new");
            await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            await adminPage.FillAsync("input[name='Title']", draftContestTitle);
            await adminPage.FillAsync("textarea[name='Summary']", "Private draft regarding secret wild sourdough hydration ratios.");
            await adminPage.FillAsync("textarea[name='DescriptionAndRules']", "Internal draft specifications not ready for public viewing. Testing draft privacy rule.");
            await adminPage.FillAsync("input[name='OpensAt']", now.AddDays(10).ToString("yyyy-MM-ddTHH:mm"));
            await adminPage.FillAsync("input[name='ClosesAt']", now.AddDays(30).ToString("yyyy-MM-ddTHH:mm"));

            await adminPage.CheckAsync("input#typeTip");
            await adminPage.CheckAsync("input#statusDraft");

            await Task.WhenAll(
                adminPage.WaitForURLAsync("**/admin/contests*"),
                adminPage.ClickAsync("form[action*='/admin/contests'] button[type='submit']")
            );
            await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // -------------------------------------------------------------
            // Step 7: Confirm draft is hidden from guest public catalog & direct URL
            // -------------------------------------------------------------
            await guestPage.GotoAsync("/contests");
            await guestPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
            var updatedListingHtml = await guestPage.ContentAsync();
            Assert.DoesNotContain(draftContestTitle, updatedListingHtml);

            var draftResponse = await guestPage.GotoAsync($"/contests/{draftContestSlug}");
            Assert.NotNull(draftResponse);
            Assert.Equal(404, draftResponse.Status);

            // -------------------------------------------------------------
            // Step 8: Admin edits the contest
            // -------------------------------------------------------------
            await adminPage.GotoAsync("/admin/contests");
            var row = adminPage.Locator($"tr:has-text('{recipeContestTitle}')");
            await row.Locator("a:has-text('Edit')").ClickAsync();
            await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            await adminPage.FillAsync("input[name='Title']", updatedRecipeTitle);
            await adminPage.FillAsync("input[name='PrizeDescription']", "Upgraded Gold Grand Award");

            await Task.WhenAll(
                adminPage.WaitForURLAsync("**/admin/contests*"),
                adminPage.ClickAsync("form[action*='/edit'] button[type='submit']")
            );
            await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Verify public detail reflects updated title and prize
            await guestPage.GotoAsync($"/contests/{expectedRecipeSlug}");
            await guestPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
            var updatedDetailHtml = await guestPage.ContentAsync();
            Assert.Contains(updatedRecipeTitle, updatedDetailHtml);
            Assert.Contains("Upgraded Gold Grand Award", updatedDetailHtml);

            // -------------------------------------------------------------
            // Step 9: Invalid date order rejection in Admin form
            // -------------------------------------------------------------
            await adminPage.GotoAsync("/admin/contests/new");
            await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            await adminPage.FillAsync("input[name='Title']", "Invalid Date Test " + runId);
            await adminPage.FillAsync("textarea[name='Summary']", "Invalid date check.");
            await adminPage.FillAsync("textarea[name='DescriptionAndRules']", "Valid long description for invalid date order testing.");
            // Closes BEFORE Opens
            await adminPage.FillAsync("input[name='OpensAt']", now.AddDays(10).ToString("yyyy-MM-ddTHH:mm"));
            await adminPage.FillAsync("input[name='ClosesAt']", now.AddDays(2).ToString("yyyy-MM-ddTHH:mm"));

            await adminPage.ClickAsync("form[action*='/admin/contests'] button[type='submit']");
            await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var errorHtml = await adminPage.ContentAsync();
            Assert.Contains("Closing date and time must be strictly later than the opening date", errorHtml);

            // -------------------------------------------------------------
            // Step 10: Non-admin write rejection
            // -------------------------------------------------------------
            var guestAdminAttempt = await guestPage.GotoAsync("/admin/contests");
            Assert.NotNull(guestAdminAttempt);
            Assert.Contains("/account/login", guestPage.Url, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await server.DisposeAsync();
        }
    }
}
