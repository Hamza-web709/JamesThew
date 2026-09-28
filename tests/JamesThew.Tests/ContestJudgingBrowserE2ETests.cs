using Microsoft.Playwright;
using Xunit;

namespace JamesThew.Tests;

public class ContestJudgingBrowserE2ETests
{
    [Fact]
    public async Task Phase5C_Automated_Browser_QA_Admin_Judging_Winner_Selection_And_Public_Announcement_Flow()
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
            adminPage.Dialog += async (_, dialog) => await dialog.AcceptAsync();

            var openStewContestSlug = "autumn-heritage-stew-showdown";
            var entryTitle = "Heritage Slow-Braised Beef Bourguignon";

            // -------------------------------------------------------------
            // Step 1: Member logs in and submits a recipe entry
            // -------------------------------------------------------------
            await memberPage.GotoAsync("/account/login");
            await memberPage.FillAsync("input[name='Email']", "member@jamesthew.com");
            await memberPage.FillAsync("input[name='Password']", "Member@Pass1234!");
            await memberPage.ClickAsync("form[action*='/account/login'] button[type='submit']");
            await memberPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            await memberPage.GotoAsync($"/contests/{openStewContestSlug}/entry");
            await memberPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            await memberPage.FillAsync("input[name='Title']", entryTitle);
            await memberPage.FillAsync("textarea[name='Summary']", "Slow-cooked artisanal beef bourguignon with wild mushrooms and pearl onions.");
            await memberPage.FillAsync("input[name='Servings']", "6");
            await memberPage.FillAsync("input[name='PrepMinutes']", "30");
            await memberPage.FillAsync("input[name='CookMinutes']", "180");
            await memberPage.FillAsync("textarea[name='IngredientsText']", "1kg Chuck Steak\n200g Smoked Pancetta\n500ml Pinot Noir\n200g Brown Mushrooms");
            await memberPage.FillAsync("textarea[name='StepsText']", "Brown the pancetta and beef in batches.\nSweat vegetables and deglaze with Pinot Noir.\nSimmer covered for three hours until fork tender.");
            await memberPage.FillAsync("textarea[name='Notes']", "Family secret recipe passed down three generations.");

            await memberPage.ClickAsync("#btnSubmitContestEntry");
            await memberPage.WaitForURLAsync("**/contests/my-entries*");
            await memberPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var myEntriesInitialHtml = await memberPage.ContentAsync();
            Assert.Contains(entryTitle, myEntriesInitialHtml);
            Assert.Contains("Status: Submitted", myEntriesInitialHtml);

            // -------------------------------------------------------------
            // Step 2: Admin logs in
            // -------------------------------------------------------------
            await adminPage.GotoAsync("/account/login");
            await adminPage.FillAsync("input[name='Email']", "admin@jamesthew.com");
            await adminPage.FillAsync("input[name='Password']", "Admin@Pass1234!");
            await adminPage.ClickAsync("form[action*='/account/login'] button[type='submit']");
            await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // -------------------------------------------------------------
            // Step 3: Admin reviews entry -> sets UnderReview with private notes
            // -------------------------------------------------------------
            await adminPage.GotoAsync("/admin/contests");
            await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Click Entries link for the Autumn Heritage Stew Showdown contest
            await adminPage.ClickAsync("tr:has-text('Autumn Heritage Stew Showdown') a:has-text('Entries')");
            await adminPage.WaitForURLAsync("**/admin/contests/*/entries*");
            await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var adminEntriesHtml = await adminPage.ContentAsync();
            Assert.Contains(entryTitle, adminEntriesHtml);
            Assert.Contains("QA Member", adminEntriesHtml);

            // Expand accordion for the entry
            await adminPage.ClickAsync("button.accordion-button");
            await adminPage.WaitForTimeoutAsync(500);

            // Fill Review form
            await adminPage.SelectOptionAsync("select[name='Status']", "2"); // 2 = UnderReview
            await adminPage.FillAsync("[name='AdminReviewNotes']", "Exceptional sauce reduction; top contender for championship.");
            await adminPage.ClickAsync("button:has-text('Save Review')");

            // Wait for success alert message
            await adminPage.Locator(".alert-success").WaitForAsync();
            var afterReviewHtml = await adminPage.ContentAsync();
            Assert.Contains("Entry evaluation updated successfully", afterReviewHtml);
            Assert.Contains("Exceptional sauce reduction", afterReviewHtml);

            // -------------------------------------------------------------
            // Step 4: Member verifies edit lock during UnderReview
            // -------------------------------------------------------------
            await memberPage.GotoAsync("/contests/my-entries");
            await memberPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
            var myEntriesReviewHtml = await memberPage.ContentAsync();
            Assert.Contains("Under Review", myEntriesReviewHtml);
            Assert.Contains("Editing locked", myEntriesReviewHtml);
            Assert.DoesNotContain("Exceptional sauce reduction", myEntriesReviewHtml); // Private note not leaked!

            await memberPage.GotoAsync($"/contests/{openStewContestSlug}/entry");
            await memberPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
            var memberFormHtml = await memberPage.ContentAsync();
            Assert.Contains("Entry Editing Locked", memberFormHtml);

            // -------------------------------------------------------------
            // Step 5: Admin closes the contest so judging/selection is valid
            // -------------------------------------------------------------
            await adminPage.GotoAsync("/admin/contests");
            await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Close the contest via Close button in the contest row
            await adminPage.ClickAsync("tr:has-text('Autumn Heritage Stew Showdown') button:has-text('Close')");
            await adminPage.Locator(".alert-success").WaitForAsync();

            var closedContestsHtml = await adminPage.ContentAsync();
            Assert.Contains("is now closed", closedContestsHtml);

            // -------------------------------------------------------------
            // Step 6: Admin selects the entry as Winner
            // -------------------------------------------------------------
            await adminPage.ClickAsync("tr:has-text('Autumn Heritage Stew Showdown') a:has-text('Entries')");
            await adminPage.WaitForURLAsync("**/admin/contests/*/entries*");
            await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Open accordion and click Select Winner
            await adminPage.ClickAsync("button.accordion-button");
            await adminPage.WaitForTimeoutAsync(500);

            await adminPage.ClickAsync("button:has-text('Select as Winner')");
            await adminPage.Locator(".alert-success").WaitForAsync();

            var afterSelectHtml = await adminPage.ContentAsync();
            Assert.Contains("has been selected as the official winner", afterSelectHtml);
            Assert.Contains("Competition Winner", afterSelectHtml);
            Assert.Contains("Announce Winner Publicly", afterSelectHtml);

            // -------------------------------------------------------------
            // Step 7: Verify Unannounced Winner Privacy for Public Guest
            // -------------------------------------------------------------
            // Guest visits announcements page
            await guestPage.GotoAsync("/announcements");
            await guestPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
            var guestAnnouncementsUnannounced = await guestPage.ContentAsync();
            Assert.DoesNotContain(entryTitle, guestAnnouncementsUnannounced);
            Assert.DoesNotContain("QA Member", guestAnnouncementsUnannounced);

            // Guest visits contest detail page
            await guestPage.GotoAsync($"/contests/{openStewContestSlug}");
            await guestPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
            var guestDetailUnannounced = await guestPage.ContentAsync();
            Assert.DoesNotContain("Official Competition Winner", guestDetailUnannounced);
            Assert.DoesNotContain(entryTitle, guestDetailUnannounced);

            // -------------------------------------------------------------
            // Step 8: Admin announces the Winner publicly
            // -------------------------------------------------------------
            await adminPage.ClickAsync("button:has-text('Announce Winner Publicly')");
            await adminPage.Locator(".alert-success").WaitForAsync();

            var afterAnnounceHtml = await adminPage.ContentAsync();
            Assert.Contains("Competition winner has been formally published", afterAnnounceHtml);
            Assert.Contains("Publicly Announced", afterAnnounceHtml);

            // -------------------------------------------------------------
            // Step 9: Guest visits /announcements and verifies Public Accolade
            // -------------------------------------------------------------
            await guestPage.GotoAsync("/announcements");
            await guestPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var guestAnnouncementsHtml = await guestPage.ContentAsync();
            Assert.Contains("Contest Winner Announcements", guestAnnouncementsHtml);
            Assert.Contains("Autumn Heritage Stew Showdown", guestAnnouncementsHtml);
            Assert.Contains(entryTitle, guestAnnouncementsHtml);
            Assert.Contains("QA Member", guestAnnouncementsHtml);
            Assert.Contains("Chef James Thew Feature Article", guestAnnouncementsHtml);

            // Verify strict privacy: NO ingredients, steps, private notes, or email leaked
            Assert.DoesNotContain("Pinot Noir", guestAnnouncementsHtml);
            Assert.DoesNotContain("Simmer covered for three hours", guestAnnouncementsHtml);
            Assert.DoesNotContain("Exceptional sauce reduction", guestAnnouncementsHtml);
            Assert.DoesNotContain("member@jamesthew.com", guestAnnouncementsHtml);

            // -------------------------------------------------------------
            // Step 10: Guest visits contest detail and sees golden Winner Banner
            // -------------------------------------------------------------
            await guestPage.GotoAsync($"/contests/{openStewContestSlug}");
            await guestPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var guestDetailHtml = await guestPage.ContentAsync();
            Assert.Contains("announcedWinnerBanner", guestDetailHtml);
            Assert.Contains("Official Competition Winner", guestDetailHtml);
            Assert.Contains(entryTitle, guestDetailHtml);
            Assert.Contains("QA Member", guestDetailHtml);
            Assert.DoesNotContain("Exceptional sauce reduction", guestDetailHtml);
            Assert.DoesNotContain("member@jamesthew.com", guestDetailHtml);

            // -------------------------------------------------------------
            // Step 11: Member checks My Entries & My Entry Detail
            // -------------------------------------------------------------
            await memberPage.GotoAsync("/contests/my-entries");
            await memberPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var memberAnnouncedEntriesHtml = await memberPage.ContentAsync();
            Assert.Contains("Winner Announced!", memberAnnouncedEntriesHtml);
            Assert.Contains("Selected as competition winner", memberAnnouncedEntriesHtml);

            await memberPage.GotoAsync($"/contests/{openStewContestSlug}/my-entry");
            await memberPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var memberAnnouncedDetailHtml = await memberPage.ContentAsync();
            Assert.Contains("Official Competition Winner Announced!", memberAnnouncedDetailHtml);
            Assert.Contains("Read-Only (Locked)", memberAnnouncedDetailHtml);

            // -------------------------------------------------------------
            // Step 12: Admin revokes the winner with explicit reason
            // -------------------------------------------------------------
            await adminPage.GotoAsync("/admin/contests");
            await adminPage.ClickAsync("tr:has-text('Autumn Heritage Stew Showdown') a:has-text('Entries')");
            await adminPage.WaitForURLAsync("**/admin/contests/*/entries*");
            await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            await adminPage.ClickAsync("button:has-text('Revoke Winner')");
            await adminPage.WaitForTimeoutAsync(500);

            await adminPage.FillAsync("#revokeWinnerModal textarea[name='reason']", "Winner requested withdrawal due to commercial conflict.");
            await adminPage.ClickAsync("#revokeWinnerModal button[type='submit']");
            await adminPage.Locator(".alert-success").WaitForAsync();

            var afterRevokeHtml = await adminPage.ContentAsync();
            Assert.Contains("Winner selection has been revoked successfully", afterRevokeHtml);
            Assert.DoesNotContain("Competition Winner", afterRevokeHtml);

            // -------------------------------------------------------------
            // Step 13: Guest revisits /announcements -> winner is removed
            // -------------------------------------------------------------
            await guestPage.GotoAsync("/announcements");
            await guestPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var guestAfterRevokeAnnouncements = await guestPage.ContentAsync();
            Assert.DoesNotContain(entryTitle, guestAfterRevokeAnnouncements);

            // Guest revisits contest detail -> banner is gone
            await guestPage.GotoAsync($"/contests/{openStewContestSlug}");
            await guestPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var guestAfterRevokeDetail = await guestPage.ContentAsync();
            Assert.DoesNotContain("announcedWinnerBanner", guestAfterRevokeDetail);
        }
        finally
        {
            await server.DisposeAsync();
        }
    }
}
