using Microsoft.Playwright;
using Xunit;

namespace JamesThew.Tests;

public class EditorialBrowserE2ETests
{
    [Fact]
    public async Task Phase4_Step1_Automated_Browser_QA_Full_Lifecycle()
    {
        var server = new BrowserTestServer();
        await server.InitializeAsync();

        try
        {
            var runId = Guid.NewGuid().ToString("N")[..6].ToLowerInvariant();
            var uniqueRecipeTitle = "Herb Butter Roast Chicken " + runId;
            var expectedRecipeSlug = "herb-butter-roast-chicken-" + runId;
            var updatedRecipeTitle = "Herb Butter & Garlic Roast Chicken " + runId;
            var draftTipTitle = "Draft Emulsion Technique " + runId;
            var draftTipSlug = "draft-emulsion-technique-" + runId;
            var membersOnlyTitle = "Royal Black Truffle Wellington " + runId;
            var membersOnlySlug = "royal-black-truffle-wellington-" + runId;

            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });

            // 1. Admin Context & Login
            var adminContext = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                IgnoreHTTPSErrors = true,
                BaseURL = server.ServerAddress
            });
            var adminPage = await adminContext.NewPageAsync();

            // 2. Guest Context for verifying unauthenticated public behavior
            var guestContext = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                IgnoreHTTPSErrors = true,
                BaseURL = server.ServerAddress
            });
            var guestPage = await guestContext.NewPageAsync();

        // -------------------------------------------------------------
        // Step 1: Login as admin
        // -------------------------------------------------------------
        await adminPage.GotoAsync("/account/login");
        await adminPage.FillAsync("input[name='Email']", "admin@jamesthew.com");
        await adminPage.FillAsync("input[name='Password']", "Admin@Pass1234!");
        await adminPage.ClickAsync("form[action*='/account/login'] button[type='submit']");
        await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Verify admin authentication (nav should show Admin link)
        var adminNav = await adminPage.Locator("a:has-text('Admin')").CountAsync();
        Assert.True(adminNav > 0, "Admin link should be visible in navbar after login.");

        // -------------------------------------------------------------
        // Step 2: Open /admin/content
        // -------------------------------------------------------------
        await adminPage.GotoAsync("/admin/content");
        await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var pageTitle = await adminPage.Locator("h1").InnerTextAsync();
        Assert.Contains("Editorial Content Management", pageTitle);

        // -------------------------------------------------------------
        // Step 3: Create one published/free recipe
        // -------------------------------------------------------------
        await adminPage.GotoAsync("/admin/content/recipes/new");
        await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await adminPage.FillAsync("input[name='Title']", uniqueRecipeTitle);
        await adminPage.FillAsync("textarea[name='Summary']", "Golden roasted chicken basted with fragrant rosemary butter.");
        await adminPage.FillAsync("input[name='Servings']", "4");
        await adminPage.FillAsync("input[name='PrepMinutes']", "15");
        await adminPage.FillAsync("input[name='CookMinutes']", "50");
        await adminPage.FillAsync("textarea[name='IngredientsText']", "1 Whole Chicken\n30g Cultured Butter\nFresh Rosemary sprigs\nCoarse Sea Salt");
        await adminPage.FillAsync("textarea[name='StepsText']", "Preheat oven to 400F.\nSeason cavity and rub skin with cultured butter.\nRoast for 50 minutes until internal temp reaches 165F.");
        await adminPage.CheckAsync("input#visibilityFree");
        await adminPage.CheckAsync("input#statusPublished");

        await Task.WhenAll(
            adminPage.WaitForURLAsync("**/admin/content*"),
            adminPage.ClickAsync("form[action*='/admin/content/recipes'] button[type='submit']")
        );
        await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Verify redirected to /admin/content with success alert
        Assert.Contains("/admin/content", adminPage.Url);
        var successAlert = await adminPage.Locator(".alert-success").InnerTextAsync();
        Assert.Contains("saved successfully", successAlert);

        // -------------------------------------------------------------
        // Step 4: Confirm it appears publicly
        // -------------------------------------------------------------
        await guestPage.GotoAsync("/recipes");
        await guestPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var catalogText = await guestPage.Locator("main").InnerTextAsync();
        Assert.Contains(uniqueRecipeTitle, catalogText);

        var recipeResponse = await guestPage.GotoAsync($"/recipes/{expectedRecipeSlug}");
        Assert.NotNull(recipeResponse);
        Assert.Equal(200, recipeResponse.Status);
        var detailText = await guestPage.Locator("main").InnerTextAsync();
        Assert.Contains(uniqueRecipeTitle, detailText);
        Assert.Contains("1 Whole Chicken", detailText);
        Assert.Contains("Fresh Rosemary sprigs", detailText);
        Assert.Contains("Preheat oven to 400F", detailText);

        // -------------------------------------------------------------
        // Step 5: Create one draft recipe or tip
        // -------------------------------------------------------------
        await adminPage.GotoAsync("/admin/content/tips/new");
        await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await adminPage.FillAsync("input[name='Title']", draftTipTitle);
        await adminPage.FillAsync("textarea[name='Summary']", "Unpublished laboratory test on yolk emulsification thresholds.");
        await adminPage.FillAsync("textarea[name='Body']", "Slowly whisk warm clarified butter into tempered egg yolks over gentle steam.");
        await adminPage.CheckAsync("input#tipVisibilityFree");
        await adminPage.CheckAsync("input#tipStatusDraft");
        await Task.WhenAll(
            adminPage.WaitForURLAsync("**/admin/content*"),
            adminPage.ClickAsync("form[action*='/admin/content/tips'] button[type='submit']")
        );
        await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

        Assert.Contains("/admin/content", adminPage.Url);

        // -------------------------------------------------------------
        // Step 6: Confirm draft content is hidden publicly
        // -------------------------------------------------------------
        await guestPage.GotoAsync("/tips");
        await guestPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var tipsCatalogText = await guestPage.Locator("main").InnerTextAsync();
        Assert.DoesNotContain(draftTipTitle, tipsCatalogText);

        var draftResponse = await guestPage.GotoAsync($"/tips/{draftTipSlug}");
        Assert.NotNull(draftResponse);
        Assert.Equal(404, draftResponse.Status);

        // -------------------------------------------------------------
        // Step 7: Create or edit one members-only item
        // -------------------------------------------------------------
        await adminPage.GotoAsync("/admin/content/recipes/new");
        await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var secretIngredient = "Aged Perigord Truffle Puree " + runId;
        await adminPage.FillAsync("input[name='Title']", membersOnlyTitle);
        await adminPage.FillAsync("textarea[name='Summary']", "Exclusive masterclass formula for prime beef tenderloin en croute.");
        await adminPage.FillAsync("textarea[name='IngredientsText']", $"{secretIngredient}\nPrime Beef Fillet\nButter Puff Pastry");
        await adminPage.FillAsync("textarea[name='StepsText']", "Sear beef in clarified butter.\nCoat generously with truffle puree.\nWrap in puff pastry and bake.");
        await adminPage.CheckAsync("input#visibilityMembersOnly");
        await adminPage.CheckAsync("input#statusPublished");
        await Task.WhenAll(
            adminPage.WaitForURLAsync("**/admin/content*"),
            adminPage.ClickAsync("form[action*='/admin/content/recipes'] button[type='submit']")
        );
        await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // -------------------------------------------------------------
        // Step 8: Confirm guests see locked/paywall content and no private body/steps are exposed
        // -------------------------------------------------------------
        await guestPage.GotoAsync($"/recipes/{membersOnlySlug}");
        await guestPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var lockedDetailText = await guestPage.Locator("main").InnerTextAsync();
        Assert.Contains(membersOnlyTitle, lockedDetailText);
        Assert.Contains("Exclusive Members-Only Recipe", lockedDetailText);
        Assert.DoesNotContain(secretIngredient, lockedDetailText);
        Assert.DoesNotContain("Coat generously with truffle puree", lockedDetailText);

        // -------------------------------------------------------------
        // Step 9: Edit the published recipe
        // -------------------------------------------------------------
        await adminPage.GotoAsync("/admin/content");
        await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Find Edit link for the published recipe
        var editButton = adminPage.Locator($"tr:has-text('{uniqueRecipeTitle}') a:has-text('Edit')");
        await editButton.ClickAsync();
        await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await adminPage.FillAsync("input[name='Title']", updatedRecipeTitle);
        await Task.WhenAll(
            adminPage.WaitForURLAsync("**/admin/content*"),
            adminPage.ClickAsync("form[action*='/admin/content/recipes'] button[type='submit']")
        );
        await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Confirm public detail displays updated title
        await guestPage.GotoAsync($"/recipes/{expectedRecipeSlug}");
        await guestPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var updatedDetailText = await guestPage.Locator("main").InnerTextAsync();
        Assert.Contains(updatedRecipeTitle, updatedDetailText);

        // -------------------------------------------------------------
        // Step 10: Remove/soft-delete the recipe
        // -------------------------------------------------------------
        await adminPage.GotoAsync("/admin/content");
        await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Click Remove button to open confirmation modal
        var removeButton = adminPage.Locator($"tr:has-text('{updatedRecipeTitle}') button[data-bs-toggle='modal']:has-text('Remove')");
        await removeButton.ClickAsync();
        await adminPage.WaitForSelectorAsync(".modal.show");

        // Submit the removal inside the open modal
        var confirmButton = adminPage.Locator(".modal.show button:has-text('Yes, Remove / Unpublish')");
        await Task.WhenAll(
            adminPage.WaitForURLAsync("**/admin/content*"),
            confirmButton.ClickAsync()
        );
        await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var unpublishAlert = await adminPage.Locator(".alert-success").InnerTextAsync();
        Assert.Contains("has been removed/unpublished", unpublishAlert);

        // -------------------------------------------------------------
        // Step 11: Confirm direct public link returns 404
        // -------------------------------------------------------------
        var removedResponse = await guestPage.GotoAsync($"/recipes/{expectedRecipeSlug}");
        Assert.NotNull(removedResponse);
        Assert.Equal(404, removedResponse.Status);

        await guestPage.GotoAsync("/recipes");
        await guestPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var catalogAfterRemove = await guestPage.Locator("main").InnerTextAsync();
        Assert.DoesNotContain(updatedRecipeTitle, catalogAfterRemove);

        // -------------------------------------------------------------
        // Step 12: Restore the recipe
        // -------------------------------------------------------------
        await adminPage.GotoAsync("/admin/content?showDeleted=true");
        await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var restoreButton = adminPage.Locator($"tr:has-text('{updatedRecipeTitle}') button:has-text('Restore')");
        await Task.WhenAll(
            adminPage.WaitForURLAsync("**/admin/content*"),
            restoreButton.ClickAsync()
        );
        await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var restoreAlert = await adminPage.Locator(".alert-success").InnerTextAsync();
        Assert.Contains("has been restored", restoreAlert);

        // -------------------------------------------------------------
        // Step 13: Confirm it becomes visible again
        // -------------------------------------------------------------
        var restoredResponse = await guestPage.GotoAsync($"/recipes/{expectedRecipeSlug}");
        Assert.NotNull(restoredResponse);
        Assert.Equal(200, restoredResponse.Status);
        var restoredDetailText = await guestPage.Locator("main").InnerTextAsync();
        Assert.Contains(updatedRecipeTitle, restoredDetailText);
        Assert.Contains("1 Whole Chicken", restoredDetailText);

        // -------------------------------------------------------------
        // Step 14: Confirm community contribution queues from Phase 3B/3C still work and are separate from editorial content
        // -------------------------------------------------------------
        await adminPage.GotoAsync("/admin/contributions");
        await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var contributionsHeading = await adminPage.Locator("h1").InnerTextAsync();
        Assert.Contains("Community Content Moderation", contributionsHeading);
        var contributionsPageText = await adminPage.Locator("main").InnerTextAsync();
        Assert.DoesNotContain(updatedRecipeTitle, contributionsPageText);

        await adminPage.GotoAsync("/admin/feedback");
        await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var feedbackHeading = await adminPage.Locator("h1").InnerTextAsync();
        Assert.Contains("Member Feedback Moderation", feedbackHeading);
        }
        finally
        {
            await server.DisposeAsync();
        }
    }
}
