using Microsoft.Playwright;
using Xunit;

namespace JamesThew.Tests;

public class AdminMediaBrowserE2ETests
{
    private static byte[] ValidPngBytes => ReadAssetBytes("wwwroot", "assets", "landing", "hero", "generated", "hero-dish-beef-wellington.png");

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
    public async Task Phase4_Step2_Automated_Browser_Media_Management_Lifecycle()
    {
        var server = new BrowserTestServer();
        await server.InitializeAsync();

        var runId = Guid.NewGuid().ToString("N")[..6].ToLowerInvariant();
        var tempImageFileName = $"qa-dish-{runId}.png";
        var tempImagePath = Path.Combine(Path.GetTempPath(), tempImageFileName);
        await File.WriteAllBytesAsync(tempImagePath, ValidPngBytes);

        var recipeTitle = "Pan-Roasted King Salmon " + runId;
        var recipeSlug = "pan-roasted-king-salmon-" + runId;
        var tipTitle = "Precision Searing Temperature " + runId;
        var tipSlug = "precision-searing-temperature-" + runId;

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });

        var adminContext = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            BaseURL = server.ServerAddress
        });
        var adminPage = await adminContext.NewPageAsync();

        var guestContext = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            BaseURL = server.ServerAddress
        });
        var guestPage = await guestContext.NewPageAsync();

        try
        {
            // -------------------------------------------------------------
            // Scenario 1: Guest cannot access Admin Media Library
            // -------------------------------------------------------------
            await guestPage.GotoAsync("/admin/media");
            await guestPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
            Assert.Contains("/account/login", guestPage.Url);

            // -------------------------------------------------------------
            // Scenario 2: Member cannot access Admin Media Library
            // -------------------------------------------------------------
            var memberContext = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                IgnoreHTTPSErrors = true,
                BaseURL = server.ServerAddress
            });
            var memberPage = await memberContext.NewPageAsync();

            var memberEmail = $"member_{runId}@jamesthew.test";
            var memberPass = "MemberTestPass1234!";

            await memberPage.GotoAsync("/account/register");
            await memberPage.FillAsync("input[name='DisplayName']", "Test Member " + runId);
            await memberPage.FillAsync("input[name='Email']", memberEmail);
            await memberPage.FillAsync("input[name='Password']", memberPass);
            await memberPage.FillAsync("input[name='ConfirmPassword']", memberPass);
            await memberPage.ClickAsync("form[action*='/account/register'] button[type='submit']");
            await memberPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            await memberPage.GotoAsync("/admin/media");
            await memberPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
            Assert.Contains("/account/access-denied", memberPage.Url);

            // -------------------------------------------------------------
            // Scenario 3: Admin Login & Access Media Library
            // -------------------------------------------------------------
            await adminPage.GotoAsync("/account/login");
            await adminPage.FillAsync("input[name='Email']", "admin@jamesthew.com");
            await adminPage.FillAsync("input[name='Password']", "Admin@Pass1234!");
            await adminPage.ClickAsync("form[action*='/account/login'] button[type='submit']");
            await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var adminNav = await adminPage.Locator("a:has-text('Admin')").CountAsync();
            Assert.True(adminNav > 0, "Admin link should appear in nav after login.");

            await adminPage.GotoAsync("/admin/media");
            await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
            var mediaTitle = await adminPage.Locator("h1").InnerTextAsync();
            Assert.Contains("Editorial Media Library", mediaTitle);

            // -------------------------------------------------------------
            // Scenario 4: Admin uploads valid image
            // -------------------------------------------------------------
            // Open upload modal and set file
            await adminPage.ClickAsync("button:has-text('Upload Image')");
            await adminPage.WaitForSelectorAsync("#uploadMediaModal.show", new PageWaitForSelectorOptions { State = WaitForSelectorState.Visible });

            await adminPage.SetInputFilesAsync("#uploadMediaModal input[type='file']", tempImagePath);
            await adminPage.ClickAsync("#uploadMediaModal button[type='submit']");
            await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Verify upload success banner
            var alertText = await adminPage.Locator(".alert-success").InnerTextAsync();
            Assert.Contains("successfully uploaded", alertText);

            // -------------------------------------------------------------
            // Scenario 5: Gallery thumbnail actual load (HTTP 200 & naturalWidth > 0)
            // -------------------------------------------------------------
            // Find thumbnail for uploaded file
            var thumbnailImg = adminPage.Locator($"img[src*='qa-dish-{runId}']").First;
            await thumbnailImg.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

            await thumbnailImg.EvaluateAsync(@"async el => {
                if (!el.complete) {
                    await new Promise(resolve => {
                        el.onload = resolve;
                        el.onerror = resolve;
                    });
                }
            }");

            var naturalWidth = await thumbnailImg.EvaluateAsync<int>("el => el.naturalWidth");
            Assert.True(naturalWidth > 0, $"Thumbnail must render with naturalWidth > 0, actual: {naturalWidth}");

            var imgSrc = await thumbnailImg.GetAttributeAsync("src");
            Assert.NotNull(imgSrc);
            Assert.Contains("/uploads/editorial/", imgSrc);

            // Verify HTTP 200 for the image URL inside the browser
            var imgStatus = await adminPage.EvaluateAsync<int>($"async () => (await fetch('{imgSrc}')).status");
            Assert.Equal(200, imgStatus);

            // -------------------------------------------------------------
            // Scenario 6: Recipe creation using Media Picker Modal
            // -------------------------------------------------------------
            await adminPage.GotoAsync("/admin/content/recipes/new");
            await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Open media picker modal
            await adminPage.ClickAsync("button:has-text('Select from Media Library')");
            await adminPage.WaitForSelectorAsync("#mediaPickerModal.show", new PageWaitForSelectorOptions { State = WaitForSelectorState.Visible });
            await adminPage.WaitForSelectorAsync("#mediaPickerGrid:not(.d-none)", new PageWaitForSelectorOptions { State = WaitForSelectorState.Visible });

            // Click "Select Image" on the uploaded item in the picker
            var selectBtn = adminPage.Locator($"#mediaPickerGrid img[src*='qa-dish-{runId}']")
                .Locator("xpath=ancestor::div[contains(@class,'card')]")
                .Locator("button:has-text('Select Image')");
            await selectBtn.ClickAsync();

            // Verify preview container becomes visible and hidden input has value
            await adminPage.WaitForSelectorAsync("#currentImagePreviewContainer.d-flex", new PageWaitForSelectorOptions { State = WaitForSelectorState.Visible });
            var hiddenUrlValue = await adminPage.Locator("#hiddenImageUrl").InputValueAsync();
            Assert.Equal(imgSrc, hiddenUrlValue);

            // Fill remaining recipe fields
            await adminPage.FillAsync("input[name='Title']", recipeTitle);
            await adminPage.FillAsync("input[name='Slug']", recipeSlug);
            await adminPage.FillAsync("textarea[name='Summary']", "Crispy pan-roasted salmon fillet with herb-infused butter emulsion.");
            await adminPage.FillAsync("input[name='Servings']", "2");
            await adminPage.FillAsync("input[name='PrepMinutes']", "10");
            await adminPage.FillAsync("input[name='CookMinutes']", "12");
            await adminPage.FillAsync("textarea[name='IngredientsText']", "2 Salmon fillets, skin on\n1 tbsp cold butter\nSea salt & white pepper");
            await adminPage.FillAsync("textarea[name='StepsText']", "Pat skin thoroughly dry.\nPlace skin side down in hot cast iron.\nSear until skin is crisp and flip for 60 seconds.");
            await adminPage.CheckAsync("input#visibilityFree");
            await adminPage.CheckAsync("input#statusPublished");
            await adminPage.ClickAsync("form[action*='/admin/content/recipes/new'] button[type='submit']");
            await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // -------------------------------------------------------------
            // Scenario 7: Tip creation using Media Picker Modal
            // -------------------------------------------------------------
            await adminPage.GotoAsync("/admin/content/tips/new");
            await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            await adminPage.ClickAsync("button:has-text('Select from Media Library')");
            await adminPage.WaitForSelectorAsync("#mediaPickerModal.show", new PageWaitForSelectorOptions { State = WaitForSelectorState.Visible });
            await adminPage.WaitForSelectorAsync("#mediaPickerGrid:not(.d-none)", new PageWaitForSelectorOptions { State = WaitForSelectorState.Visible });

            var tipSelectBtn = adminPage.Locator($"#mediaPickerGrid img[src*='qa-dish-{runId}']")
                .Locator("xpath=ancestor::div[contains(@class,'card')]")
                .Locator("button:has-text('Select Image')");
            await tipSelectBtn.ClickAsync();

            await adminPage.WaitForSelectorAsync("#currentImagePreviewContainer.d-flex", new PageWaitForSelectorOptions { State = WaitForSelectorState.Visible });
            await adminPage.FillAsync("input[name='Title']", tipTitle);
            await adminPage.FillAsync("input[name='Slug']", tipSlug);
            await adminPage.FillAsync("textarea[name='Summary']", "Mastering the smoke point and sizzle temperature for perfect protein searing.");
            await adminPage.FillAsync("textarea[name='Body']", "Always ensure the pan is preheated before adding oil. Look for shimmering oil and the faint whisper of smoke before introducing proteins.");
            await adminPage.CheckAsync("input#tipVisibilityFree");
            await adminPage.CheckAsync("input#tipStatusPublished");
            await adminPage.ClickAsync("form[action*='/admin/content/tips/new'] button[type='submit']");
            await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // -------------------------------------------------------------
            // Scenario 8: Public page renders image correctly (HTTP 200 & naturalWidth > 0)
            // -------------------------------------------------------------
            // Public recipe detail
            await guestPage.GotoAsync($"/recipes/{recipeSlug}");
            await guestPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
            Assert.Contains(recipeTitle, await guestPage.Locator("h1").InnerTextAsync());

            var recipeImg = guestPage.Locator($"img[src='{imgSrc}']");
            await recipeImg.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
            await recipeImg.EvaluateAsync(@"async el => {
                if (!el.complete) {
                    await new Promise(resolve => {
                        el.onload = resolve;
                        el.onerror = resolve;
                    });
                }
            }");
            var recipeImgWidth = await recipeImg.EvaluateAsync<int>("el => el.naturalWidth");
            Assert.True(recipeImgWidth > 0, $"Public recipe image must render with naturalWidth > 0, actual: {recipeImgWidth}");

            // Public tip detail
            await guestPage.GotoAsync($"/tips/{tipSlug}");
            await guestPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
            Assert.Contains(tipTitle, await guestPage.Locator("h1").InnerTextAsync());

            var tipImg = guestPage.Locator($"img[src='{imgSrc}']");
            await tipImg.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
            await tipImg.EvaluateAsync(@"async el => {
                if (!el.complete) {
                    await new Promise(resolve => {
                        el.onload = resolve;
                        el.onerror = resolve;
                    });
                }
            }");
            var tipImgWidth = await tipImg.EvaluateAsync<int>("el => el.naturalWidth");
            Assert.True(tipImgWidth > 0, $"Public tip image must render with naturalWidth > 0, actual: {tipImgWidth}");

            // -------------------------------------------------------------
            // Scenario 9: Image Unlink flow on recipe edit
            // -------------------------------------------------------------
            await adminPage.GotoAsync("/admin/content");
            await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Find recipe and edit
            var editRecipeLink = adminPage.Locator($"tr:has-text('{recipeTitle}') a:has-text('Edit')").First;
            await editRecipeLink.ClickAsync();
            await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Click "Remove Image" button
            await adminPage.ClickAsync("#btnClearImage");
            var previewHidden = await adminPage.Locator("#currentImagePreviewContainer.d-none").CountAsync();
            Assert.True(previewHidden > 0, "Preview container should become hidden after clicking Remove Image.");

            var removeImageVal = await adminPage.Locator("#hiddenRemoveImage").InputValueAsync();
            Assert.Equal("true", removeImageVal);

            // Save changes
            await adminPage.ClickAsync("button[type='submit']:has-text('Save Changes')");
            await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Public recipe now renders without image tag
            await guestPage.GotoAsync($"/recipes/{recipeSlug}");
            await guestPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
            var remainingImgCount = await guestPage.Locator($"img[src='{imgSrc}']").CountAsync();
            Assert.Equal(0, remainingImgCount);

            // -------------------------------------------------------------
            // Scenario 10: In-use media deletion flow with safe auto-unlinking
            // -------------------------------------------------------------
            // Image is still referenced by the cooking tip!
            await adminPage.GotoAsync("/admin/media");
            await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // In Use badge should show In Use (1)
            var mediaCard = adminPage.Locator($".card:has-text('qa-dish-{runId}')");
            var inUseBadge = await mediaCard.Locator(".badge.bg-success:has-text('In Use')").InnerTextAsync();
            Assert.Contains("In Use (1)", inUseBadge);

            // Click delete button
            await mediaCard.Locator("button[title='Delete File']").ClickAsync();

            // Modal appears with safe deletion notice
            var modalId = (await mediaCard.Locator("button[title='Delete File']").GetAttributeAsync("data-bs-target"))!;
            await adminPage.WaitForSelectorAsync($"{modalId}.show", new PageWaitForSelectorOptions { State = WaitForSelectorState.Visible });
            var modalNotice = await adminPage.Locator($"{modalId} .alert-warning").InnerTextAsync();
            Assert.Contains("Safe Deletion Notice", modalNotice);
            Assert.Contains("referenced by 1 content item", modalNotice);

            // Confirm delete
            await adminPage.ClickAsync($"{modalId} button:has-text('Confirm Delete')");
            await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var deleteSuccessMsg = await adminPage.Locator(".alert-success").InnerTextAsync();
            Assert.Contains("safely removed", deleteSuccessMsg);
            Assert.Contains("Unlinked from 1 content item", deleteSuccessMsg);

            // Verify public tip still returns 200 OK without broken image tag
            await guestPage.GotoAsync($"/tips/{tipSlug}");
            await guestPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
            Assert.Contains(tipTitle, await guestPage.Locator("h1").InnerTextAsync());
            var tipImgAfterDelete = await guestPage.Locator($"img[src='{imgSrc}']").CountAsync();
            Assert.Equal(0, tipImgAfterDelete);
        }
        finally
        {
            if (File.Exists(tempImagePath))
            {
                try { File.Delete(tempImagePath); } catch { }
            }

            await server.DisposeAsync();
        }
    }
}
