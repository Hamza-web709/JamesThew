using Microsoft.Playwright;
using Xunit;

namespace JamesThew.Tests;

public class ReleaseReadinessBrowserE2ETests
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
    public async Task Phase6A_Automated_EndToEnd_ReleaseReadiness_And_Responsive_Audit()
    {
        var server = new BrowserTestServer();
        await server.InitializeAsync();

        var runId = server.RunId;
        var screenshotsDir = Path.Combine(Path.GetTempPath(), $"JamesThew_QA_Screenshots_{runId}");
        Directory.CreateDirectory(screenshotsDir);

        try
        {
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });

            var consoleErrors = new List<string>();

            void AttachPageMonitors(IPage page, string pageLabel)
            {
                page.PageError += (_, msg) => consoleErrors.Add($"[{pageLabel} Error]: {msg}");
                page.Response += (_, res) =>
                {
                    if (res.Status >= 500)
                    {
                        consoleErrors.Add($"[{pageLabel} HTTP 500]: {res.Url} (Status {res.Status})");
                    }
                };
                page.Console += (_, msg) =>
                {
                    if (msg.Type == "error")
                    {
                        // Ignore harmless favicon 404 if any
                        if (!msg.Text.Contains("favicon"))
                        {
                            consoleErrors.Add($"[{pageLabel} Console Error]: {msg.Text}");
                        }
                    }
                };
            }

            // =========================================================================
            // PART 1: GUEST BROWSING & RESPONSIVENESS (DESKTOP & MOBILE 390px)
            // =========================================================================
            var desktopGuestContext = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                IgnoreHTTPSErrors = true,
                BaseURL = server.ServerAddress,
                ViewportSize = new ViewportSize { Width = 1280, Height = 800 }
            });
            var desktopGuestPage = await desktopGuestContext.NewPageAsync();
            AttachPageMonitors(desktopGuestPage, "DesktopGuest");

            var mobileGuestContext = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                IgnoreHTTPSErrors = true,
                BaseURL = server.ServerAddress,
                ViewportSize = new ViewportSize { Width = 390, Height = 844 },
                IsMobile = true,
                HasTouch = true
            });
            var mobileGuestPage = await mobileGuestContext.NewPageAsync();
            AttachPageMonitors(mobileGuestPage, "MobileGuest");

            // Helper to verify responsive layout: no horizontal overflow and valid images
            async Task AssertPageLayoutHealthy(IPage page, string url, string screenshotName)
            {
                await page.GotoAsync(url, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded,
                    Timeout = 90000
                });
                await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

                // Verify no horizontal overflow
                var hasOverflow = await page.EvaluateAsync<bool>(@"() => {
                    return document.documentElement.scrollWidth > window.innerWidth + 2;
                }");
                Assert.False(hasOverflow, $"Horizontal overflow detected at {url} on viewport {page.ViewportSize?.Width}x{page.ViewportSize?.Height}");

                // Verify all images rendered properly
                var brokenImagesCount = await page.EvaluateAsync<int>(@"() => {
                    const imgs = Array.from(document.querySelectorAll('img'));
                    return imgs.filter(img => img.complete && img.naturalWidth === 0 && img.src && !img.src.includes('data:')).length;
                }");
                Assert.Equal(0, brokenImagesCount);

                // Capture screenshot to temp directory
                var path = Path.Combine(screenshotsDir, screenshotName);
                await page.ScreenshotAsync(new PageScreenshotOptions { Path = path, FullPage = true });
            }

            // Test guest routes on desktop and mobile
            await AssertPageLayoutHealthy(desktopGuestPage, "/", "desktop_01_home.png");
            await AssertPageLayoutHealthy(mobileGuestPage, "/", "mobile_01_home.png");

            await AssertPageLayoutHealthy(desktopGuestPage, "/recipes", "desktop_02_recipes.png");
            await AssertPageLayoutHealthy(mobileGuestPage, "/recipes", "mobile_02_recipes.png");

            await AssertPageLayoutHealthy(desktopGuestPage, "/tips", "desktop_03_tips.png");
            await AssertPageLayoutHealthy(mobileGuestPage, "/tips", "mobile_03_tips.png");

            await AssertPageLayoutHealthy(desktopGuestPage, "/faq", "desktop_04_faq.png");
            await AssertPageLayoutHealthy(mobileGuestPage, "/faq", "mobile_04_faq.png");

            await AssertPageLayoutHealthy(desktopGuestPage, "/announcements", "desktop_05_announcements.png");
            await AssertPageLayoutHealthy(mobileGuestPage, "/announcements", "mobile_05_announcements.png");

            await AssertPageLayoutHealthy(desktopGuestPage, "/contests", "desktop_06_contests.png");
            await AssertPageLayoutHealthy(mobileGuestPage, "/contests", "mobile_06_contests.png");

            // Verify Free Content access vs Members-Only Content lock for Guest
            await desktopGuestPage.GotoAsync("/recipes/jamess-classic-roast-herb-chicken");
            await desktopGuestPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
            var freeIngredientsHeader = await desktopGuestPage.Locator(".jt-ingredients-panel").CountAsync();
            Assert.True(freeIngredientsHeader > 0, "Free recipe must display ingredients panel to guest.");

            await desktopGuestPage.GotoAsync("/recipes/jamess-masterclass-beef-wellington");
            await desktopGuestPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
            var paidLockBanner = await desktopGuestPage.Locator(".jt-locked-box").CountAsync();
            Assert.True(paidLockBanner > 0, "Members-only recipe must show subscription barrier to guest.");
            var lockedIngredients = await desktopGuestPage.Locator(".jt-ingredients-panel").CountAsync();
            Assert.Equal(0, lockedIngredients);

            // =========================================================================
            // PART 2: REGISTRATION & LOGIN FLOW
            // =========================================================================
            var memberContext = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                IgnoreHTTPSErrors = true,
                BaseURL = server.ServerAddress
            });
            var memberPage = await memberContext.NewPageAsync();
            AttachPageMonitors(memberPage, "Member");

            var memberEmail = $"alex_{runId}@jamesthew.test";
            var memberPass = "MemberTestPass1234!";

            await TestAuth.RegisterBrowserMemberAsync(memberPage, "Alex Rivers", memberEmail, memberPass);
            await memberPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Landed on account status as free registered member
            Assert.Contains("/account/status", memberPage.Url);
            var memberWelcome = await memberPage.Locator("h1").InnerTextAsync();
            Assert.Contains("Account", memberWelcome);

            // =========================================================================
            // PART 3: DEMO PAYMENT MEMBERSHIP ACTIVATION FLOW
            // =========================================================================
            // Alex completes the academic demo checkout and payment OTP.
            await memberPage.GotoAsync("/membership/checkout?plan=Monthly");
            await memberPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            await memberPage.FillAsync("input[name='CardholderName']", "Alex Rivers");
            await memberPage.FillAsync("input[name='CardNumber']", "4242 4242 4242 4242");
            await memberPage.FillAsync("input[name='Expiry']", "12/30");
            await memberPage.FillAsync("input[name='Cvv']", "123");
            await memberPage.ClickAsync("form[action*='/membership/checkout'] button[type='submit']");
            await memberPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
            Assert.Contains("/membership/payment-otp", memberPage.Url);

            await TestAuth.CompleteBrowserDemoPaymentOtpAsync(memberPage);
            await memberPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
            Assert.Contains("/membership", memberPage.Url);

            var activeBadge = await memberPage.Locator("text=Active Premium Access").CountAsync();
            Assert.True(activeBadge > 0, "Alex must see active premium access after demo checkout.");

            // Admin context logs in for later moderation checks.
            var adminContext = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                IgnoreHTTPSErrors = true,
                BaseURL = server.ServerAddress
            });
            var adminPage = await adminContext.NewPageAsync();
            adminPage.Dialog += async (_, d) => await d.AcceptAsync();
            AttachPageMonitors(adminPage, "Admin");

            await adminPage.GotoAsync("/account/login");
            await adminPage.FillAsync("input[name='Email']", "admin@jamesthew.com");
            await adminPage.FillAsync("input[name='Password']", "Admin@Pass1234!");
            await adminPage.ClickAsync("form[action*='/account/login'] button[type='submit']");
            await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Alex refreshes members-only recipe: now fully unlocked!
            await memberPage.GotoAsync("/recipes/jamess-masterclass-beef-wellington");
            await memberPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
            var unlockedIngredients = await memberPage.Locator(".jt-ingredients-panel").CountAsync();
            Assert.True(unlockedIngredients > 0, "Approved subscriber must have full access to ingredients & proportions.");

            // =========================================================================
            // PART 4: COMMUNITY CONTRIBUTION & ADMIN MODERATION FLOW
            // =========================================================================
            await memberPage.GotoAsync("/contributions/recipe/new");
            await memberPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var recipeTitle = $"Alex Golden Saffron Broth {runId}";
            await memberPage.FillAsync("input[name='Title']", recipeTitle);
            await memberPage.FillAsync("textarea[name='Summary']", "A delicate golden broth with saffron and garden herbs.");
            await memberPage.FillAsync("input[name='Servings']", "4");
            await memberPage.FillAsync("input[name='PrepMinutes']", "15");
            await memberPage.FillAsync("input[name='CookMinutes']", "30");
            await memberPage.FillAsync("textarea[name='IngredientsText']", "Saffron threads\nVegetable broth\nSea salt");
            await memberPage.FillAsync("textarea[name='StepsText']", "1. Simmer broth\n2. Steep saffron for 10 minutes");
            await memberPage.ClickAsync("#btnSubmitRecipe");
            await memberPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Verify recipe is listed as Pending in member's dashboard
            Assert.Contains("/contributions", memberPage.Url);
            var pendingRecipeRow = await memberPage.Locator($"tr:has-text('{recipeTitle}')").CountAsync();
            Assert.True(pendingRecipeRow > 0);

            // Admin moderates and approves recipe
            await adminPage.GotoAsync("/admin/contributions");
            await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var adminContribRow = adminPage.Locator($"tr:not(.collapse):has-text('{recipeTitle}')");
            await adminContribRow.WaitForAsync();
            await adminContribRow.Locator("button:has-text('Inspect')").ClickAsync();
            await adminPage.WaitForTimeoutAsync(500);
            await adminPage.Locator("button:has-text('Approve & Publish')").First.ClickAsync();
            await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Guest visits public recipes: community recipe is now publicly published!
            await desktopGuestPage.GotoAsync("/recipes");
            await desktopGuestPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
            var publishedCard = await desktopGuestPage.Locator($"text={recipeTitle}").CountAsync();
            Assert.True(publishedCard > 0, "Approved community contribution must be visible in public recipe catalog.");

            // =========================================================================
            // PART 5: EDITORIAL MEDIA UPLOAD FLOW
            // =========================================================================
            await adminPage.GotoAsync("/admin/media");
            await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Create a small test image in temp
            var tempImagePath = Path.Combine(Path.GetTempPath(), $"qa-img-{runId}.png");
            await File.WriteAllBytesAsync(tempImagePath, ValidPngBytes);

            await adminPage.ClickAsync("button:has-text('Upload Image')");
            await adminPage.WaitForTimeoutAsync(500);
            await adminPage.SetInputFilesAsync("#uploadMediaModal input[type='file']", tempImagePath);
            await adminPage.ClickAsync("#uploadMediaModal button[type='submit']");
            await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var mediaSuccessAlert = await adminPage.Locator(".alert-success").InnerTextAsync();
            Assert.Contains("successfully uploaded", mediaSuccessAlert);

            // =========================================================================
            // PART 6: CONTEST ENTRY, JUDGING, ANNOUNCEMENT & REVOCATION FLOW
            // =========================================================================
            var contestSlug = "autumn-heritage-stew-showdown";
            var contestEntryTitle = $"Braised Highland Short Ribs {runId}";

            // Alex enters contest
            await memberPage.GotoAsync($"/contests/{contestSlug}/entry");
            await memberPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            await memberPage.FillAsync("input[name='Title']", contestEntryTitle);
            await memberPage.FillAsync("textarea[name='Summary']", "Slow-cooked heritage beef with rich root vegetables and red wine reduction.");
            await memberPage.FillAsync("input[name='Servings']", "4");
            await memberPage.FillAsync("input[name='PrepMinutes']", "30");
            await memberPage.FillAsync("input[name='CookMinutes']", "180");
            await memberPage.FillAsync("textarea[name='IngredientsText']", "800g Prime Beef Short Ribs\n2 tbsp Olive Oil\n1 tsp Sea Salt");
            await memberPage.FillAsync("textarea[name='StepsText']", "1. Sear short ribs deeply on all sides in a heavy Dutch oven.\n2. Braise low and slow for 3 hours.");
            await memberPage.ClickAsync("#btnSubmitContestEntry");
            await memberPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Admin closes contest to enable winner selection
            await adminPage.GotoAsync("/admin/contests");
            await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await adminPage.ClickAsync("tr:has-text('Autumn Heritage Stew Showdown') button:has-text('Close')");
            await adminPage.Locator(".alert-success").WaitForAsync();

            // Admin navigates to judging inbox
            await adminPage.ClickAsync("tr:has-text('Autumn Heritage Stew Showdown') a:has-text('Entries')");
            await adminPage.WaitForURLAsync("**/admin/contests/*/entries*");
            await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Open accordion and click Select Winner
            await adminPage.ClickAsync("button.accordion-button");
            await adminPage.WaitForTimeoutAsync(500);

            await adminPage.ClickAsync("button:has-text('Select as Winner')");
            await adminPage.Locator(".alert-success").WaitForAsync();

            // Guest visits announcements: unannounced winner is NOT visible
            await desktopGuestPage.GotoAsync("/announcements");
            await desktopGuestPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
            var unannouncedCheck = await desktopGuestPage.Locator($"text={contestEntryTitle}").CountAsync();
            Assert.Equal(0, unannouncedCheck);

            // Admin clicks Announce Winner
            await adminPage.ClickAsync("button:has-text('Announce Winner Publicly')");
            await adminPage.Locator(".alert-success").WaitForAsync();

            // Guest visits announcements: golden accolade is proudly published!
            await desktopGuestPage.GotoAsync("/announcements");
            await desktopGuestPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
            var announcedAccolade = await desktopGuestPage.Locator($"text={contestEntryTitle}").CountAsync();
            Assert.True(announcedAccolade > 0, "Public announcements must display announced winner entry.");
            var announcedAuthor = await desktopGuestPage.Locator("text=Alex Rivers").CountAsync();
            Assert.True(announcedAuthor > 0, "Announcements must show entrant display name.");

            // Verify ZERO private data leak in public HTML
            var pageSource = await desktopGuestPage.ContentAsync();
            Assert.DoesNotContain(memberEmail, pageSource);
            Assert.DoesNotContain("Exceptional depth of flavor", pageSource);

            // Admin revokes winner with explicit durable reason
            var revokeReason = "Eligibility compliance review requested.";
            await adminPage.ClickAsync("button:has-text('Revoke Winner')");
            await adminPage.WaitForTimeoutAsync(500);
            await adminPage.FillAsync("#revokeWinnerModal textarea[name='reason']", revokeReason);
            await adminPage.ClickAsync("#revokeWinnerModal button[type='submit']");
            await adminPage.Locator(".alert-success").WaitForAsync();

            // Guest visits announcements: accolade is immediately withdrawn!
            await desktopGuestPage.GotoAsync("/announcements");
            await desktopGuestPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
            var revokedAccolade = await desktopGuestPage.Locator($"text={contestEntryTitle}").CountAsync();
            Assert.Equal(0, revokedAccolade);

            // Admin entries page displays durable revocation audit banner
            await adminPage.GotoAsync(adminPage.Url);
            await adminPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
            var durableAuditBanner = await adminPage.Locator($"text={revokeReason}").CountAsync();
            Assert.True(durableAuditBanner > 0, "Durable revocation audit banner must be displayed in admin judging view.");

            // =========================================================================
            // PART 7: ZERO UNHANDLED CONSOLE / SERVER ERRORS
            // =========================================================================
            Assert.True(consoleErrors.Count == 0, $"Console errors encountered:\n{string.Join("\n", consoleErrors)}");

            // Cleanup temp file
            if (File.Exists(tempImagePath))
            {
                File.Delete(tempImagePath);
            }
        }
        finally
        {
            await server.DisposeAsync();

            if (Directory.Exists(screenshotsDir))
            {
                // Preserve screenshot artifacts in temp without tracking in Git
            }
        }
    }
}
