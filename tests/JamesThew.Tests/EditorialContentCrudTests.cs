using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using JamesThew.Data;
using JamesThew.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace JamesThew.Tests;

[Collection("Foundation")]
public class EditorialContentCrudTests(FoundationFixture fixture)
{
    private static string NewEmail() => Guid.NewGuid().ToString("N") + "@example.test";
    private static string NewPassword() => Convert.ToHexString(RandomNumberGenerator.GetBytes(20)) + "a!9";

    private static async Task<string> Token(HttpClient client, string path)
    {
        var html = await client.GetStringAsync(path);
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, $"Expected an antiforgery token in the rendered form at {path}.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static async Task RegisterAndLogin(HttpClient client, string email, string password, string displayName = "Test Member")
    {
        var values = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await Token(client, "/account/register"),
            ["DisplayName"] = displayName,
            ["Email"] = email,
            ["Password"] = password,
            ["ConfirmPassword"] = password
        };
        var registerResponse = await client.PostAsync("/account/register", new FormUrlEncodedContent(values));
        Assert.Equal(HttpStatusCode.Redirect, registerResponse.StatusCode);
    }

    private static async Task Login(HttpClient client, string email, string password)
    {
        var values = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await Token(client, "/account/login"),
            ["Email"] = email,
            ["Password"] = password,
            ["ReturnUrl"] = "/"
        };
        var response = await client.PostAsync("/account/login", new FormUrlEncodedContent(values));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static IConfiguration SeedConfig(string email, string password) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["LocalAdmin:Email"] = email,
            ["LocalAdmin:Password"] = password
        }).Build();

    private sealed class DevEnv : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "JamesThew";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        var client = fixture.NewClient();
        var adminEmail = NewEmail();
        var adminPassword = NewPassword();
        using (var scope = fixture.Services.CreateScope())
        {
            var env = new DevEnv();
            var config = SeedConfig(adminEmail, adminPassword);
            await IdentitySeeder.SeedLocalAdminAsync(scope.ServiceProvider, config, env);
        }
        await Login(client, adminEmail, adminPassword);
        return client;
    }

    [Fact]
    public async Task Guest_access_to_editorial_content_admin_is_redirected_to_login()
    {
        using var client = fixture.NewClient();

        var getCatalogResponse = await client.GetAsync("/admin/content");
        Assert.Equal(HttpStatusCode.Redirect, getCatalogResponse.StatusCode);
        Assert.Contains("/account/login", getCatalogResponse.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);

        var getNewRecipeResponse = await client.GetAsync("/admin/content/recipes/new");
        Assert.Equal(HttpStatusCode.Redirect, getNewRecipeResponse.StatusCode);
        Assert.Contains("/account/login", getNewRecipeResponse.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);

        var getNewTipResponse = await client.GetAsync("/admin/content/tips/new");
        Assert.Equal(HttpStatusCode.Redirect, getNewTipResponse.StatusCode);
        Assert.Contains("/account/login", getNewTipResponse.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);

        var postValues = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = "mock-token",
            ["Title"] = "Unauthorized Recipe"
        };
        var postResponse = await client.PostAsync("/admin/content/recipes/new", new FormUrlEncodedContent(postValues));
        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);
        Assert.Contains("/account/login", postResponse.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Member_access_to_editorial_content_admin_is_denied()
    {
        using var client = fixture.NewClient();
        await RegisterAndLogin(client, NewEmail(), NewPassword(), "Regular Member");

        var response = await client.GetAsync("/admin/content");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/account/access-denied", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);

        var recipeNewResponse = await client.GetAsync("/admin/content/recipes/new");
        Assert.Equal(HttpStatusCode.Redirect, recipeNewResponse.StatusCode);
        Assert.Contains("/account/access-denied", recipeNewResponse.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Admin_can_create_free_recipe_and_it_appears_in_public_catalog_and_detail()
    {
        using var adminClient = await CreateAdminClientAsync();
        var uniqueTitle = "Artisanal Pan-Seared Salmon " + Guid.NewGuid().ToString("N")[..6];

        var token = await Token(adminClient, "/admin/content/recipes/new");
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Title"] = uniqueTitle,
            ["Slug"] = "",
            ["Summary"] = "Fresh Atlantic salmon pan-seared with crispy skin and lemon dill butter.",
            ["ImageUrl"] = "/images/recipes/salmon.jpg",
            ["Servings"] = "2",
            ["PrepMinutes"] = "10",
            ["CookMinutes"] = "12",
            ["IngredientsText"] = "2 Salmon fillets, skin-on\n1 tbsp Olive oil\n20g Butter\nFresh dill\nSea salt",
            ["StepsText"] = "Pat salmon dry with paper towel.\nSeason skin with sea salt.\nSear skin-side down for 6 minutes.\nBaste with butter and dill.",
            ["Visibility"] = ContentVisibility.Free.ToString(),
            ["PublicationStatus"] = PublicationStatus.Published.ToString()
        };

        var postResponse = await adminClient.PostAsync("/admin/content/recipes/new", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);
        Assert.Contains("/admin/content", postResponse.Headers.Location?.OriginalString);

        // Verify in database
        await using var db = fixture.CreateDb();
        var item = await db.ContentItems
            .Include(i => i.Recipe)
                .ThenInclude(r => r!.Ingredients)
            .Include(i => i.Recipe)
                .ThenInclude(r => r!.Steps)
            .FirstOrDefaultAsync(i => i.Title == uniqueTitle);

        Assert.NotNull(item);
        Assert.Equal(ContentOrigin.Editorial, item.Origin);
        Assert.Equal(ContentKind.Recipe, item.Kind);
        Assert.Equal(ContentVisibility.Free, item.Visibility);
        Assert.Equal(PublicationStatus.Published, item.PublicationStatus);
        Assert.NotNull(item.Recipe);
        Assert.Equal(5, item.Recipe.Ingredients.Count);
        Assert.Equal(4, item.Recipe.Steps.Count);

        // Verify public access as unauthenticated guest
        using var guestClient = fixture.NewClient();
        var catalogResponse = await guestClient.GetStringAsync("/recipes");
        Assert.Contains(uniqueTitle, catalogResponse);

        var detailResponse = await guestClient.GetStringAsync($"/recipes/{item.Slug}");
        Assert.Contains(uniqueTitle, detailResponse);
        Assert.Contains("Salmon fillets, skin-on", detailResponse);
        Assert.Contains("Pat salmon dry with paper towel.", detailResponse);
    }

    [Fact]
    public async Task Admin_can_create_members_only_recipe_and_guest_sees_locked_preview()
    {
        using var adminClient = await CreateAdminClientAsync();
        var secretIngredient = "Rare Beluga Truffle Essence " + Guid.NewGuid().ToString("N")[..6];
        var uniqueTitle = "Grand Masterclass Duck " + Guid.NewGuid().ToString("N")[..6];

        var token = await Token(adminClient, "/admin/content/recipes/new");
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Title"] = uniqueTitle,
            ["Summary"] = "Elite masterclass culinary creation reserved for subscription members.",
            ["IngredientsText"] = $"{secretIngredient}\nDuck breast\nCognac",
            ["StepsText"] = "Slowly infuse cognac with the essence for 48 hours.\nGlaze the duck breast.",
            ["Visibility"] = ContentVisibility.MembersOnly.ToString(),
            ["PublicationStatus"] = PublicationStatus.Published.ToString()
        };

        var postResponse = await adminClient.PostAsync("/admin/content/recipes/new", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);

        await using var db = fixture.CreateDb();
        var item = await db.ContentItems.FirstAsync(i => i.Title == uniqueTitle);

        // Guest checks detail page
        using var guestClient = fixture.NewClient();
        var guestHtml = await guestClient.GetStringAsync($"/recipes/{item.Slug}");
        Assert.Contains(uniqueTitle, guestHtml);
        Assert.Contains("Exclusive Members-Only", guestHtml);
        // Secret ingredient and full steps must NEVER be visible in anonymous response
        Assert.DoesNotContain(secretIngredient, guestHtml);
        Assert.DoesNotContain("Slowly infuse cognac", guestHtml);

        // Admin checks detail page -> Admin can view full recipe
        var adminHtml = await adminClient.GetStringAsync($"/recipes/{item.Slug}");
        Assert.Contains(secretIngredient, adminHtml);
    }

    [Fact]
    public async Task Admin_can_edit_existing_editorial_recipe()
    {
        using var adminClient = await CreateAdminClientAsync();
        var initialTitle = "Original Roast Chicken " + Guid.NewGuid().ToString("N")[..6];
        var updatedTitle = "Classic French Roast Chicken " + Guid.NewGuid().ToString("N")[..6];

        // Create
        var token = await Token(adminClient, "/admin/content/recipes/new");
        var createForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Title"] = initialTitle,
            ["Summary"] = "Golden roasted whole chicken with herbed butter.",
            ["PrepMinutes"] = "15",
            ["CookMinutes"] = "60",
            ["IngredientsText"] = "1 Whole chicken\nButter\nHerbs",
            ["StepsText"] = "Preheat oven.\nRoast chicken.",
            ["Visibility"] = ContentVisibility.Free.ToString(),
            ["PublicationStatus"] = PublicationStatus.Published.ToString()
        };
        await adminClient.PostAsync("/admin/content/recipes/new", new FormUrlEncodedContent(createForm));

        await using var db = fixture.CreateDb();
        var recipe = await db.ContentItems.FirstAsync(i => i.Title == initialTitle);

        // Edit
        var editToken = await Token(adminClient, $"/admin/content/recipes/{recipe.Id}/edit");
        var editForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = editToken,
            ["Id"] = recipe.Id.ToString(),
            ["Title"] = updatedTitle,
            ["Slug"] = recipe.Slug,
            ["Summary"] = "Refined French technique for golden roasted whole chicken.",
            ["PrepMinutes"] = "25",
            ["CookMinutes"] = "75",
            ["IngredientsText"] = "1 Whole heritage chicken\nFrench cultured butter\nTarragon & Thyme",
            ["StepsText"] = "Truss the chicken tightly.\nBaste continuously with melted butter.\nRest before carving.",
            ["Visibility"] = ContentVisibility.Free.ToString(),
            ["PublicationStatus"] = PublicationStatus.Published.ToString()
        };

        var editResponse = await adminClient.PostAsync($"/admin/content/recipes/{recipe.Id}/edit", new FormUrlEncodedContent(editForm));
        Assert.Equal(HttpStatusCode.Redirect, editResponse.StatusCode);

        // Verify public view reflects update
        using var guestClient = fixture.NewClient();
        var updatedHtml = await guestClient.GetStringAsync($"/recipes/{recipe.Slug}");
        Assert.Contains(updatedTitle, updatedHtml);
        Assert.Contains("French cultured butter", updatedHtml);
        Assert.Contains("Truss the chicken tightly", updatedHtml);
    }

    [Fact]
    public async Task Admin_can_create_and_edit_editorial_tip()
    {
        using var adminClient = await CreateAdminClientAsync();
        var tipTitle = "Knife Sharpening Fundamentals " + Guid.NewGuid().ToString("N")[..6];
        var secretTechnique = "Maintain a steady 15-degree angle against the 1000-grit whetstone.";

        // Create Tip
        var token = await Token(adminClient, "/admin/content/tips/new");
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Title"] = tipTitle,
            ["Summary"] = "Mastering the Japanese water stone technique for razor sharpness.",
            ["Body"] = secretTechnique,
            ["Visibility"] = ContentVisibility.Free.ToString(),
            ["PublicationStatus"] = PublicationStatus.Published.ToString()
        };

        var postResponse = await adminClient.PostAsync("/admin/content/tips/new", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);

        await using var db = fixture.CreateDb();
        var tip = await db.ContentItems.FirstAsync(i => i.Title == tipTitle);
        Assert.Equal(ContentKind.Tip, tip.Kind);
        Assert.Equal(ContentOrigin.Editorial, tip.Origin);

        // Public view
        using var guestClient = fixture.NewClient();
        var tipHtml = await guestClient.GetStringAsync($"/tips/{tip.Slug}");
        Assert.Contains(tipTitle, tipHtml);
        Assert.Contains(secretTechnique, tipHtml);

        // Edit Tip
        var editToken = await Token(adminClient, $"/admin/content/tips/{tip.Id}/edit");
        var updatedTechnique = "Maintain a 15-degree angle and finish with a leather strop for a mirror bevel.";
        var editForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = editToken,
            ["Id"] = tip.Id.ToString(),
            ["Title"] = tipTitle,
            ["Slug"] = tip.Slug,
            ["Summary"] = "Mastering the Japanese water stone technique with strop finishing.",
            ["Body"] = updatedTechnique,
            ["Visibility"] = ContentVisibility.Free.ToString(),
            ["PublicationStatus"] = PublicationStatus.Published.ToString()
        };
        var editResponse = await adminClient.PostAsync($"/admin/content/tips/{tip.Id}/edit", new FormUrlEncodedContent(editForm));
        Assert.Equal(HttpStatusCode.Redirect, editResponse.StatusCode);

        var updatedHtml = await guestClient.GetStringAsync($"/tips/{tip.Slug}");
        Assert.Contains(updatedTechnique, updatedHtml);
    }

    [Fact]
    public async Task Admin_draft_content_is_hidden_from_public_catalog_and_slug_returns_404()
    {
        using var adminClient = await CreateAdminClientAsync();
        var draftTitle = "Secret Test Recipe In Progress " + Guid.NewGuid().ToString("N")[..6];

        var token = await Token(adminClient, "/admin/content/recipes/new");
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Title"] = draftTitle,
            ["Summary"] = "Unfinished experimental kitchen notes not ready for public release.",
            ["IngredientsText"] = "Flour\nWater",
            ["StepsText"] = "Mix and observe fermentation rate.",
            ["Visibility"] = ContentVisibility.Free.ToString(),
            ["PublicationStatus"] = PublicationStatus.Draft.ToString()
        };

        var postResponse = await adminClient.PostAsync("/admin/content/recipes/new", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);

        await using var db = fixture.CreateDb();
        var draftItem = await db.ContentItems.FirstAsync(i => i.Title == draftTitle);
        Assert.Equal(PublicationStatus.Draft, draftItem.PublicationStatus);

        // Public check: NOT in catalog, direct URL returns 404
        using var guestClient = fixture.NewClient();
        var catalogHtml = await guestClient.GetStringAsync("/recipes");
        Assert.DoesNotContain(draftTitle, catalogHtml);

        var directResponse = await guestClient.GetAsync($"/recipes/{draftItem.Slug}");
        Assert.Equal(HttpStatusCode.NotFound, directResponse.StatusCode);
    }

    [Fact]
    public async Task Duplicate_slugs_are_automatically_resolved_with_unique_suffix()
    {
        using var adminClient = await CreateAdminClientAsync();
        var baseTitle = "Crispy Duck Confit " + Guid.NewGuid().ToString("N")[..4];

        // 1st item
        var token1 = await Token(adminClient, "/admin/content/recipes/new");
        var form1 = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token1,
            ["Title"] = baseTitle,
            ["Summary"] = "Traditional duck confit slowly cured in duck fat.",
            ["IngredientsText"] = "Duck legs\nDuck fat",
            ["StepsText"] = "Submerge and cook low for 3 hours.",
            ["Visibility"] = ContentVisibility.Free.ToString(),
            ["PublicationStatus"] = PublicationStatus.Published.ToString()
        };
        await adminClient.PostAsync("/admin/content/recipes/new", new FormUrlEncodedContent(form1));

        // 2nd item with same title
        var token2 = await Token(adminClient, "/admin/content/recipes/new");
        var form2 = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token2,
            ["Title"] = baseTitle,
            ["Summary"] = "Modern quick-pan variation of duck confit.",
            ["IngredientsText"] = "Duck legs\nDuck fat",
            ["StepsText"] = "Crisp skin under broiler.",
            ["Visibility"] = ContentVisibility.Free.ToString(),
            ["PublicationStatus"] = PublicationStatus.Published.ToString()
        };
        await adminClient.PostAsync("/admin/content/recipes/new", new FormUrlEncodedContent(form2));

        await using var db = fixture.CreateDb();
        var items = await db.ContentItems.Where(i => i.Title == baseTitle).OrderBy(i => i.Id).ToListAsync();
        Assert.Equal(2, items.Count);
        Assert.NotEqual(items[0].Slug, items[1].Slug);
        Assert.EndsWith("-2", items[1].Slug);

        // Both URLs resolve independently
        using var guestClient = fixture.NewClient();
        var resp1 = await guestClient.GetAsync($"/recipes/{items[0].Slug}");
        var resp2 = await guestClient.GetAsync($"/recipes/{items[1].Slug}");
        Assert.Equal(HttpStatusCode.OK, resp1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, resp2.StatusCode);
    }

    [Fact]
    public async Task Admin_soft_delete_unpublishes_content_and_restore_brings_it_back()
    {
        using var adminClient = await CreateAdminClientAsync();
        var title = "Seasonal Mushroom Tart " + Guid.NewGuid().ToString("N")[..6];

        var token = await Token(adminClient, "/admin/content/recipes/new");
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Title"] = title,
            ["Summary"] = "Flaky puff pastry tart filled with sauteed wild chanterelles and thyme.",
            ["IngredientsText"] = "Puff pastry\nChanterelles\nGruyere",
            ["StepsText"] = "Bake pastry.\nTop with sauteed mushrooms.",
            ["Visibility"] = ContentVisibility.Free.ToString(),
            ["PublicationStatus"] = PublicationStatus.Published.ToString()
        };
        await adminClient.PostAsync("/admin/content/recipes/new", new FormUrlEncodedContent(form));

        await using var db = fixture.CreateDb();
        var item = await db.ContentItems.FirstAsync(i => i.Title == title);

        // Confirmed live
        using var guestClient = fixture.NewClient();
        var initialResp = await guestClient.GetAsync($"/recipes/{item.Slug}");
        Assert.Equal(HttpStatusCode.OK, initialResp.StatusCode);

        // Admin soft-deletes via POST /admin/content/{id}/remove
        var removeToken = await Token(adminClient, "/admin/content");
        var removeForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = removeToken
        };
        var removeResp = await adminClient.PostAsync($"/admin/content/{item.Id}/remove", new FormUrlEncodedContent(removeForm));
        Assert.Equal(HttpStatusCode.Redirect, removeResp.StatusCode);

        // Public check: 404 and gone from catalog
        var deletedResp = await guestClient.GetAsync($"/recipes/{item.Slug}");
        Assert.Equal(HttpStatusCode.NotFound, deletedResp.StatusCode);

        var catalogHtml = await guestClient.GetStringAsync("/recipes");
        Assert.DoesNotContain(title, catalogHtml);

        // Admin restores via POST /admin/content/{id}/restore
        var restoreToken = await Token(adminClient, "/admin/content?showDeleted=true");
        var restoreForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = restoreToken
        };
        var restoreResp = await adminClient.PostAsync($"/admin/content/{item.Id}/restore", new FormUrlEncodedContent(restoreForm));
        Assert.Equal(HttpStatusCode.Redirect, restoreResp.StatusCode);

        // Public check: restored and accessible again
        var restoredResp = await guestClient.GetAsync($"/recipes/{item.Slug}");
        Assert.Equal(HttpStatusCode.OK, restoredResp.StatusCode);
    }

    [Fact]
    public async Task Server_side_validation_rejects_missing_required_fields()
    {
        using var adminClient = await CreateAdminClientAsync();
        var token = await Token(adminClient, "/admin/content/recipes/new");

        // Missing Title, Summary, Ingredients, Steps
        var invalidForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Title"] = "",
            ["Summary"] = "",
            ["IngredientsText"] = "",
            ["StepsText"] = ""
        };

        var response = await adminClient.PostAsync("/admin/content/recipes/new", new FormUrlEncodedContent(invalidForm));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // Returns view with errors
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Recipe title is required.", html);
        Assert.Contains("Recipe summary is required.", html);
    }

    [Fact]
    public async Task Community_contributions_are_isolated_from_editorial_crud()
    {
        // 1. Member submits a community recipe contribution
        using var memberClient = fixture.NewClient();
        var memberEmail = NewEmail();
        await RegisterAndLogin(memberClient, memberEmail, NewPassword(), "Culinary Fan");

        var token = await Token(memberClient, "/contributions/recipe/new");
        var contribTitle = "Home Cook Grandma Pie " + Guid.NewGuid().ToString("N")[..6];
        var contribForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Title"] = contribTitle,
            ["Summary"] = "Grandma's secret crispy pan pizza passed down through generations.",
            ["IngredientsText"] = "Flour\nYeast\nTomato sauce\nMozzarella",
            ["StepsText"] = "Proof dough for 24 hours.\nPress into oiled square sheet pan.\nBake at 500F."
        };
        var submitResp = await memberClient.PostAsync("/contributions/recipe/new", new FormUrlEncodedContent(contribForm));
        Assert.Equal(HttpStatusCode.Redirect, submitResp.StatusCode);

        await using var db = fixture.CreateDb();
        var communityItem = await db.ContentItems.FirstAsync(i => i.Title == contribTitle);
        Assert.Equal(ContentOrigin.Community, communityItem.Origin);
        Assert.Equal(PublicationStatus.Pending, communityItem.PublicationStatus);

        // 2. Admin editorial catalog list MUST NOT display community items
        using var adminClient = await CreateAdminClientAsync();
        var editorialCatalogHtml = await adminClient.GetStringAsync("/admin/content");
        Assert.DoesNotContain(contribTitle, editorialCatalogHtml);

        // 3. Editorial remove endpoint MUST reject community items
        var removeToken = await Token(adminClient, "/admin/content");
        var removeForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = removeToken
        };
        var removeAttempt = await adminClient.PostAsync($"/admin/content/{communityItem.Id}/remove", new FormUrlEncodedContent(removeForm));
        Assert.Equal(HttpStatusCode.Redirect, removeAttempt.StatusCode);

        // Confirm community item was NOT soft-deleted
        var freshItem = await db.ContentItems.AsNoTracking().FirstAsync(i => i.Id == communityItem.Id);
        Assert.Null(freshItem.DeletedAtUtc);

        // 4. Community item is properly visible in community moderation queue
        var moderationHtml = await adminClient.GetStringAsync("/admin/contributions");
        Assert.Contains(contribTitle, moderationHtml);
    }
}
