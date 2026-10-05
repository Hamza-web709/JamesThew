using Microsoft.Playwright;
using Xunit;

namespace JamesThew.Tests;

public class ProfileBrowserE2ETests
{
    [Fact]
    public async Task REQ005_SamePage_Profile_View_And_Edit_Browser_Verification()
    {
        var server = new BrowserTestServer();
        await server.InitializeAsync();

        try
        {
            var runId = server.RunId;
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });

            var memberContext = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                IgnoreHTTPSErrors = true,
                BaseURL = server.ServerAddress,
                ViewportSize = new ViewportSize { Width = 1280, Height = 800 }
            });
            var memberPage = await memberContext.NewPageAsync();

            var memberEmail = $"chef_{runId}@jamesthew.test";
            var memberPass = "ProfileTestPass1234!";

            // Step 1: Register new member
            await TestAuth.RegisterBrowserMemberAsync(memberPage, "Original Browser Chef", memberEmail, memberPass);
            await memberPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Step 2: Landed on /account/status or /account/profile
            Assert.True(memberPage.Url.Contains("/account/status") || memberPage.Url.Contains("/account/profile"), $"Expected account status or profile URL, got: {memberPage.Url}");
            var pageHeading = await memberPage.Locator("h1").InnerTextAsync();
            Assert.Contains("Account", pageHeading);

            // Verify form elements exist on the same page
            var formExists = await memberPage.Locator("#formEditProfile").CountAsync();
            Assert.Equal(1, formExists);

            var tokenCount = await memberPage.Locator("#formEditProfile input[name='__RequestVerificationToken']").CountAsync();
            Assert.True(tokenCount >= 1, "Antiforgery token must be present in profile edit form.");

            var currentDisplayNameVal = await memberPage.Locator("#inputDisplayName").InputValueAsync();
            Assert.Equal("Original Browser Chef", currentDisplayNameVal);

            var emailVal = await memberPage.Locator("#inputEmail").InputValueAsync();
            Assert.Equal(memberEmail, emailVal);

            // Step 3: Test server-side validation error via browser (input too short: 1 character)
            await memberPage.FillAsync("#inputDisplayName", "X");
            await memberPage.ClickAsync("#btnUpdateProfile");
            await memberPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var errorCount = await memberPage.Locator(".text-danger").CountAsync();
            Assert.True(errorCount > 0, "Validation message must be displayed when display name is invalid.");
            var pageContentAfterInvalid = await memberPage.ContentAsync();
            Assert.Contains("Display name must be between 2 and 100 characters", pageContentAfterInvalid);

            // Step 4: Test valid profile update via browser
            await memberPage.FillAsync("#inputDisplayName", "Executive Chef James Rivers");
            await memberPage.ClickAsync("#btnUpdateProfile");
            await memberPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Verify success banner and updated value on the same page
            var successBanner = await memberPage.Locator("#alertProfileSuccess").CountAsync();
            Assert.Equal(1, successBanner);
            var successText = await memberPage.Locator("#alertProfileSuccess").InnerTextAsync();
            Assert.Contains("profile has been updated successfully", successText);

            var updatedDisplayNameVal = await memberPage.Locator("#inputDisplayName").InputValueAsync();
            Assert.Equal("Executive Chef James Rivers", updatedDisplayNameVal);

            // Step 5: Verify alternate route /account/profile also loads the same view
            await memberPage.GotoAsync("/account/profile");
            await memberPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
            var profilePageName = await memberPage.Locator("#inputDisplayName").InputValueAsync();
            Assert.Equal("Executive Chef James Rivers", profilePageName);
        }
        finally
        {
            await server.DisposeAsync();
        }
    }
}
