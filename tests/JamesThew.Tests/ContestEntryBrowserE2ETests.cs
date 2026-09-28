using Microsoft.Playwright;
using Xunit;

namespace JamesThew.Tests;

public class ContestEntryBrowserE2ETests
{
    [Fact]
    public async Task Phase5B_Automated_Browser_QA_Contest_Entry_And_My_Entries_Flow()
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

            // 1. Guest Context
            var guestContext = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                IgnoreHTTPSErrors = true,
                BaseURL = server.ServerAddress
            });
            var guestPage = await guestContext.NewPageAsync();

            // 2. Member Context
            var memberContext = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                IgnoreHTTPSErrors = true,
                BaseURL = server.ServerAddress
            });
            var memberPage = await memberContext.NewPageAsync();

            // 3. Admin Context
            var adminContext = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                IgnoreHTTPSErrors = true,
                BaseURL = server.ServerAddress
            });
            var adminPage = await adminContext.NewPageAsync();

            var openStewContestSlug = "autumn-heritage-stew-showdown";
            var entryTitleOriginal = "Braised Shin of Beef and Winter Mirepoix";
            var entryTitleUpdated = "Braised Shin of Beef and Glazed Parsnips (Refined)";

            // -------------------------------------------------------------
            // Step 1: Guest visits open contest detail page
            // -------------------------------------------------------------
            await guestPage.GotoAsync($"/contests/{openStewContestSlug}");
            await guestPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var guestDetailHtml = await guestPage.ContentAsync();
            Assert.Contains("Autumn Heritage Stew Showdown", guestDetailHtml);
            Assert.Contains("Log In to Prepare Entry", guestDetailHtml);
            Assert.DoesNotContain("Submit Your Official Entry", guestDetailHtml);

            // -------------------------------------------------------------
            // Step 2: Member logs in
            // -------------------------------------------------------------
            await memberPage.GotoAsync("/account/login");
            await memberPage.FillAsync("input[name='Email']", "member@jamesthew.com");
            await memberPage.FillAsync("input[name='Password']", "Member@Pass1234!");
            await memberPage.ClickAsync("form[action*='/account/login'] button[type='submit']");
            await memberPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var myEntriesNav = await memberPage.Locator("a:has-text('My Entries')").CountAsync();
            Assert.True(myEntriesNav > 0, "My Entries navigation link must be visible for authenticated member.");

            // -------------------------------------------------------------
            // Step 3: Member views open contest and navigates to entry form
            // -------------------------------------------------------------
            await memberPage.GotoAsync($"/contests/{openStewContestSlug}");
            await memberPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var memberDetailHtml = await memberPage.ContentAsync();
            Assert.Contains("Authenticated Member Account Active", memberDetailHtml);
            Assert.Contains("Submit Your Official Entry", memberDetailHtml);

            await memberPage.ClickAsync("#btnEnterContest");
            await memberPage.WaitForURLAsync($"**/{openStewContestSlug}/entry*");
            await memberPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var entryFormHtml = await memberPage.ContentAsync();
            Assert.Contains("Official Competition Entry", entryFormHtml);
            Assert.Contains("Recipe Details", entryFormHtml);

            // -------------------------------------------------------------
            // Step 4: Member fills and submits recipe entry
            // -------------------------------------------------------------
            await memberPage.FillAsync("input[name='Title']", entryTitleOriginal);
            await memberPage.FillAsync("textarea[name='Summary']", "Slow-simmered beef shank with roasted parsnips and rich red wine jus.");
            await memberPage.FillAsync("input[name='Servings']", "4");
            await memberPage.FillAsync("input[name='PrepMinutes']", "20");
            await memberPage.FillAsync("input[name='CookMinutes']", "150");
            await memberPage.FillAsync("textarea[name='IngredientsText']", "800g Beef Shin\n2 Parsnips, quartered\n500ml Dark Beef Stock\n200ml Full-Bodied Red Wine");
            await memberPage.FillAsync("textarea[name='StepsText']", "Sear the beef shin until deeply browned.\nAdd parsnips and sweat with aromatics.\nDeglaze with wine and braise slowly for 2.5 hours.");
            await memberPage.FillAsync("textarea[name='Notes']", "Serve hot with crusty artisanal sourdough.");

            await Task.WhenAll(
                memberPage.WaitForURLAsync("**/contests/my-entries*"),
                memberPage.ClickAsync("#btnSubmitContestEntry")
            );
            await memberPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // -------------------------------------------------------------
            // Step 5: Verify "My Contest Entries" renders the submitted entry
            // -------------------------------------------------------------
            var myEntriesHtml = await memberPage.ContentAsync();
            Assert.Contains("My Contest Entries", myEntriesHtml);
            Assert.Contains(entryTitleOriginal, myEntriesHtml);
            Assert.Contains("Autumn Heritage Stew Showdown", myEntriesHtml);
            Assert.Contains("Contest Open", myEntriesHtml);
            Assert.Contains("Status: Submitted", myEntriesHtml);

            // -------------------------------------------------------------
            // Step 6: Member views their full entry detail
            // -------------------------------------------------------------
            await memberPage.ClickAsync($"a[href*='/{openStewContestSlug}/my-entry']");
            await memberPage.WaitForURLAsync($"**/{openStewContestSlug}/my-entry*");
            await memberPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var myDetailHtml = await memberPage.ContentAsync();
            Assert.Contains(entryTitleOriginal, myDetailHtml);
            Assert.Contains("800g Beef Shin", myDetailHtml);
            Assert.Contains("200ml Full-Bodied Red Wine", myDetailHtml);
            Assert.Contains("Deglaze with wine and braise slowly", myDetailHtml);
            Assert.Contains("Serve hot with crusty artisanal sourdough.", myDetailHtml);

            // -------------------------------------------------------------
            // Step 7: Member edits their entry before closing deadline
            // -------------------------------------------------------------
            await memberPage.ClickAsync($"a[href*='/{openStewContestSlug}/entry']");
            await memberPage.WaitForURLAsync($"**/{openStewContestSlug}/entry*");
            await memberPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Verify form is pre-populated
            var editFormTitle = await memberPage.InputValueAsync("input[name='Title']");
            Assert.Equal(entryTitleOriginal, editFormTitle);

            // Update title and notes
            await memberPage.FillAsync("input[name='Title']", entryTitleUpdated);
            await memberPage.FillAsync("textarea[name='Notes']", "Refined with butter-glazed parsnips.");

            await Task.WhenAll(
                memberPage.WaitForURLAsync("**/contests/my-entries*"),
                memberPage.ClickAsync("#btnSubmitContestEntry")
            );
            await memberPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var updatedMyEntriesHtml = await memberPage.ContentAsync();
            Assert.Contains(entryTitleUpdated, updatedMyEntriesHtml);

            // -------------------------------------------------------------
            // Step 8: Public guest verifies NO entry leakage
            // -------------------------------------------------------------
            await guestPage.GotoAsync($"/contests/{openStewContestSlug}");
            await guestPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
            var guestPageHtml = await guestPage.ContentAsync();
            Assert.DoesNotContain(entryTitleOriginal, guestPageHtml);
            Assert.DoesNotContain(entryTitleUpdated, guestPageHtml);

            await guestPage.GotoAsync("/contests");
            await guestPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
            var guestCatalogHtml = await guestPage.ContentAsync();
            Assert.DoesNotContain(entryTitleOriginal, guestCatalogHtml);
            Assert.DoesNotContain(entryTitleUpdated, guestCatalogHtml);

            // -------------------------------------------------------------
            // Step 9: Admin logs in and inspects submissions read-only
            // -------------------------------------------------------------
            await adminPage.GotoAsync("/account/login");
            await adminPage.FillAsync("input[name='Email']", "admin@jamesthew.com");
            await adminPage.FillAsync("input[name='Password']", "Admin@Pass1234!");
            await adminPage.ClickAsync("form[action*='/account/login'] button[type='submit']");
            await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Open Admin Contests
            await adminPage.GotoAsync("/admin/contests");
            await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var adminContestsHtml = await adminPage.ContentAsync();
            Assert.Contains("Entries (1)", adminContestsHtml);

            // Click Entries (1) link
            await adminPage.ClickAsync("a:has-text('Entries (1)')");
            await adminPage.WaitForURLAsync("**/admin/contests/*/entries*");
            await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var adminEntriesPageHtml = await adminPage.ContentAsync();
            Assert.Contains("Submissions: Autumn Heritage Stew Showdown", adminEntriesPageHtml);
            Assert.Contains("Total Submissions", adminEntriesPageHtml);
            Assert.Contains(entryTitleUpdated, adminEntriesPageHtml);
            Assert.Contains("QA Member", adminEntriesPageHtml);
            Assert.Contains("member@jamesthew.com", adminEntriesPageHtml);
            Assert.Contains("Read-Only Intake Review", adminEntriesPageHtml);

            // Expand accordion item
            await adminPage.ClickAsync("button.accordion-button");
            var accordionBody = await adminPage.Locator("div.accordion-collapse").First.InnerTextAsync();
            Assert.Contains("800g Beef Shin", accordionBody);
            Assert.Contains("Refined with butter-glazed parsnips.", accordionBody);
        }
        finally
        {
            await server.DisposeAsync();
        }
    }
}
