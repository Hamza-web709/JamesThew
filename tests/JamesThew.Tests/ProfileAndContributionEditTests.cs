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
public class ProfileAndContributionEditTests(FoundationFixture fixture)
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

    private async Task EnsureAdminSeeded(string adminEmail, string adminPassword)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        await IdentitySeeder.SeedLocalAdminAsync(scope.ServiceProvider, SeedConfig(adminEmail, adminPassword), new DevEnv());
    }

    // -------------------------------------------------------------
    // REQ-005: Same-Page Profile Viewing & Editing Tests
    // -------------------------------------------------------------

    [Fact]
    public async Task Profile_View_RendersSamePageForm_AndDetails()
    {
        var email = NewEmail();
        var password = NewPassword();
        using var client = fixture.NewClient();

        await RegisterAndLogin(client, email, password, "Initial Chef Name");

        // Both /account/status and /account/profile are accessible
        var html = await client.GetStringAsync("/account/status");
        Assert.Contains("Initial Chef Name", html);
        Assert.Contains(email, html);
        Assert.Contains("formEditProfile", html);
        Assert.Contains("btnUpdateProfile", html);
        Assert.Contains("does not grant paid membership", html);

        var profileHtml = await client.GetStringAsync("/account/profile");
        Assert.Contains("Initial Chef Name", profileHtml);
        Assert.Contains("formEditProfile", profileHtml);
    }

    [Fact]
    public async Task Profile_Update_ValidDisplayName_PersistsAndRedirects()
    {
        var email = NewEmail();
        var password = NewPassword();
        using var client = fixture.NewClient();

        await RegisterAndLogin(client, email, password, "Old Name");

        var token = await Token(client, "/account/status");
        var formValues = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["DisplayName"] = "Updated Master Chef"
        };

        var postResponse = await client.PostAsync("/account/status", new FormUrlEncodedContent(formValues));
        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);
        Assert.True(postResponse.Headers.Location!.OriginalString is "/account/status" or "/account/profile");

        // Follow redirect and verify updated name
        var updatedHtml = await client.GetStringAsync(postResponse.Headers.Location!.OriginalString);
        Assert.Contains("Updated Master Chef", updatedHtml);
        Assert.Contains("profile has been updated successfully", updatedHtml);
    }

    [Fact]
    public async Task Profile_Update_InvalidDisplayName_ShowsValidationError()
    {
        var email = NewEmail();
        var password = NewPassword();
        using var client = fixture.NewClient();

        await RegisterAndLogin(client, email, password, "Valid Name");

        var token = await Token(client, "/account/status");
        var formValues = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["DisplayName"] = "A" // Too short (< 2 characters)
        };

        var postResponse = await client.PostAsync("/account/status", new FormUrlEncodedContent(formValues));
        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);

        var html = await postResponse.Content.ReadAsStringAsync();
        Assert.Contains("Display name must be between 2 and 100 characters", html);
    }

    [Fact]
    public async Task Profile_Update_RequiresAntiforgeryToken()
    {
        var email = NewEmail();
        var password = NewPassword();
        using var client = fixture.NewClient();

        await RegisterAndLogin(client, email, password, "Name One");

        var formValues = new Dictionary<string, string>
        {
            ["DisplayName"] = "Hacked Name Without Token"
        };

        var postResponse = await client.PostAsync("/account/status", new FormUrlEncodedContent(formValues));
        Assert.Equal(HttpStatusCode.BadRequest, postResponse.StatusCode);
    }

    // -------------------------------------------------------------
    // REQ-012: Member Contribution Edit & Moderation Lifecycle Tests
    // -------------------------------------------------------------

    [Fact]
    public async Task Contribution_Recipe_Edit_PrepopulatesAndUpdates_WhilePending()
    {
        var email = NewEmail();
        var password = NewPassword();
        using var client = fixture.NewClient();

        await RegisterAndLogin(client, email, password, "Culinary Creator");

        // Submit initial recipe
        var token = await Token(client, "/contributions/recipe/new");
        var createValues = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Title"] = "Drafting Wild Mushroom Risotto",
            ["Summary"] = "Initial summary of mushroom risotto dish.",
            ["Servings"] = "2",
            ["PrepMinutes"] = "15",
            ["CookMinutes"] = "20",
            ["IngredientsText"] = "200g Arborio rice\n500ml chicken stock",
            ["StepsText"] = "Saute onions in butter.\nSimmer rice adding stock gradually.",
            ["Notes"] = "First draft notes."
        };
        var createResponse = await client.PostAsync("/contributions/recipe/new", new FormUrlEncodedContent(createValues));
        Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);

        // Find the created item ID
        await using var db = fixture.CreateDb();
        var item = await db.ContentItems.Include(c => c.Recipe).FirstAsync(c => c.Title == "Drafting Wild Mushroom Risotto");
        var itemId = item.Id;

        // GET edit form
        var editFormHtml = await client.GetStringAsync($"/contributions/recipe/{itemId}/edit");
        Assert.Contains("Drafting Wild Mushroom Risotto", editFormHtml);
        Assert.Contains("200g Arborio rice", editFormHtml);
        Assert.Contains("Saute onions in butter", editFormHtml);

        // POST update
        var editToken = await Token(client, $"/contributions/recipe/{itemId}/edit");
        var updateValues = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = editToken,
            ["Title"] = "Refined Wild Forest Mushroom Risotto",
            ["Summary"] = "Updated rich summary of wild porcini risotto.",
            ["Servings"] = "4",
            ["PrepMinutes"] = "20",
            ["CookMinutes"] = "30",
            ["IngredientsText"] = "300g Carnaroli rice\n1L rich stock\n100g dried porcini",
            ["StepsText"] = "Rehydrate porcini mushrooms.\nToast Carnaroli in olive oil.\nAdd stock in small additions.",
            ["Notes"] = "Refined notes."
        };
        var editResponse = await client.PostAsync($"/contributions/recipe/{itemId}/edit", new FormUrlEncodedContent(updateValues));
        Assert.Equal(HttpStatusCode.Redirect, editResponse.StatusCode);

        // Verify database reflects updates and remains Pending
        await using var verifyDb = fixture.CreateDb();
        var updatedItem = await verifyDb.ContentItems
            .Include(c => c.Recipe)
                .ThenInclude(r => r!.Ingredients)
            .Include(c => c.Recipe)
                .ThenInclude(r => r!.Steps)
            .FirstAsync(c => c.Id == itemId);

        Assert.Equal("Refined Wild Forest Mushroom Risotto", updatedItem.Title);
        Assert.Equal(PublicationStatus.Pending, updatedItem.PublicationStatus);
        Assert.Equal(3, updatedItem.Recipe!.Ingredients.Count);
        Assert.Equal(3, updatedItem.Recipe!.Steps.Count);
    }

    [Fact]
    public async Task Contribution_Recipe_Edit_WhenPublished_ResetsToPending_HidesFromPublic()
    {
        var authorEmail = NewEmail();
        var authorPassword = NewPassword();
        using var authorClient = fixture.NewClient();

        await RegisterAndLogin(authorClient, authorEmail, authorPassword, "Published Author");

        // Submit recipe
        var token = await Token(authorClient, "/contributions/recipe/new");
        var values = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Title"] = "Artisanal Sourdough Focaccia",
            ["Summary"] = "Fragrant rosemary focaccia recipe.",
            ["Servings"] = "6",
            ["PrepMinutes"] = "30",
            ["CookMinutes"] = "25",
            ["IngredientsText"] = "500g strong flour\n350ml water\n10g sea salt",
            ["StepsText"] = "Mix and knead dough.\nProve overnight in fridge.\nBake at 220C."
        };
        await authorClient.PostAsync("/contributions/recipe/new", new FormUrlEncodedContent(values));

        // Get item ID and slug
        await using var db = fixture.CreateDb();
        var item = await db.ContentItems.FirstAsync(c => c.Title == "Artisanal Sourdough Focaccia");
        var itemId = item.Id;
        var slug = item.Slug;

        // Admin approves and publishes the recipe
        var adminEmail = NewEmail();
        var adminPassword = NewPassword();
        await EnsureAdminSeeded(adminEmail, adminPassword);
        using var adminClient = fixture.NewClient();
        await Login(adminClient, adminEmail, adminPassword);

        var approveToken = await Token(adminClient, $"/admin/contributions");
        var approveValues = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = approveToken,
            ["visibility"] = "Free"
        };
        var approveResponse = await adminClient.PostAsync($"/admin/contributions/{itemId}/approve", new FormUrlEncodedContent(approveValues));
        Assert.Equal(HttpStatusCode.Redirect, approveResponse.StatusCode);

        // Verify it is public (HTTP 200 on direct route and in /recipes catalog)
        using var publicClient = fixture.NewClient();
        var publicRecipeResponse = await publicClient.GetAsync($"/recipes/{slug}");
        Assert.Equal(HttpStatusCode.OK, publicRecipeResponse.StatusCode);

        // Author now EDITS the published recipe
        var editToken = await Token(authorClient, $"/contributions/recipe/{itemId}/edit");
        var editValues = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = editToken,
            ["Title"] = "Artisanal Sourdough Focaccia Rev 2",
            ["Summary"] = "Updated with roasted garlic.",
            ["Servings"] = "8",
            ["PrepMinutes"] = "35",
            ["CookMinutes"] = "30",
            ["IngredientsText"] = "500g strong flour\n350ml water\n10g salt\n1 head roasted garlic",
            ["StepsText"] = "Mix ingredients with roasted garlic.\nProve and bake."
        };
        var editResponse = await authorClient.PostAsync($"/contributions/recipe/{itemId}/edit", new FormUrlEncodedContent(editValues));
        Assert.Equal(HttpStatusCode.Redirect, editResponse.StatusCode);

        // Moderation Lifecycle Verification:
        // 1. Status is reset to Pending in database
        await using var verifyDb = fixture.CreateDb();
        var editedItem = await verifyDb.ContentItems.FirstAsync(c => c.Id == itemId);
        Assert.Equal(PublicationStatus.Pending, editedItem.PublicationStatus);

        // 2. The recipe is IMMEDIATELY HIDDEN from public catalog / direct slug returns 404
        var hiddenPublicResponse = await publicClient.GetAsync($"/recipes/{slug}");
        Assert.Equal(HttpStatusCode.NotFound, hiddenPublicResponse.StatusCode);
    }

    [Fact]
    public async Task Contribution_Recipe_Edit_CrossMember_Denied()
    {
        var authorEmail = NewEmail();
        var authorPassword = NewPassword();
        using var authorClient = fixture.NewClient();
        await RegisterAndLogin(authorClient, authorEmail, authorPassword, "Author A");

        // Author A submits a recipe
        var token = await Token(authorClient, "/contributions/recipe/new");
        var values = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Title"] = "Author A Exclusive Recipe",
            ["Summary"] = "Secret family recipe.",
            ["IngredientsText"] = "Ingredient 1\nIngredient 2",
            ["StepsText"] = "Step 1\nStep 2"
        };
        await authorClient.PostAsync("/contributions/recipe/new", new FormUrlEncodedContent(values));

        await using var db = fixture.CreateDb();
        var item = await db.ContentItems.FirstAsync(c => c.Title == "Author A Exclusive Recipe");
        var itemId = item.Id;

        // Attacker B logs in
        var attackerEmail = NewEmail();
        var attackerPassword = NewPassword();
        using var attackerClient = fixture.NewClient();
        await RegisterAndLogin(attackerClient, attackerEmail, attackerPassword, "Attacker B");

        // Attacker B attempts to GET edit form of Author A's recipe -> 404 Not Found
        var getResponse = await attackerClient.GetAsync($"/contributions/recipe/{itemId}/edit");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);

        // Attacker B attempts to POST edit to Author A's recipe -> 404 Not Found
        var attackerToken = await Token(attackerClient, "/contributions");
        var maliciousValues = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = attackerToken,
            ["Title"] = "Defaced by Attacker B",
            ["Summary"] = "Defaced summary.",
            ["IngredientsText"] = "Poison 1",
            ["StepsText"] = "Do bad stuff."
        };
        var postResponse = await attackerClient.PostAsync($"/contributions/recipe/{itemId}/edit", new FormUrlEncodedContent(maliciousValues));
        Assert.Equal(HttpStatusCode.NotFound, postResponse.StatusCode);

        // Verify Author A's recipe remains unchanged
        await using var verifyDb = fixture.CreateDb();
        var safeItem = await verifyDb.ContentItems.FirstAsync(c => c.Id == itemId);
        Assert.Equal("Author A Exclusive Recipe", safeItem.Title);
    }

    [Fact]
    public async Task Contribution_Tip_Edit_PrepopulatesAndUpdates()
    {
        var email = NewEmail();
        var password = NewPassword();
        using var client = fixture.NewClient();

        await RegisterAndLogin(client, email, password, "Tip Contributor");

        // Submit cooking tip
        var token = await Token(client, "/contributions/tip/new");
        var createValues = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Title"] = "Cold Butter Pan Sauce Emulsion",
            ["Summary"] = "Initial technique summary.",
            ["Body"] = "Always swirl diced cold butter off the flame to form a silky glossy emulsion."
        };
        var createResponse = await client.PostAsync("/contributions/tip/new", new FormUrlEncodedContent(createValues));
        Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);

        await using var db = fixture.CreateDb();
        var item = await db.ContentItems.Include(c => c.Tip).FirstAsync(c => c.Title == "Cold Butter Pan Sauce Emulsion");
        var itemId = item.Id;

        // GET edit form
        var editHtml = await client.GetStringAsync($"/contributions/tip/{itemId}/edit");
        Assert.Contains("Cold Butter Pan Sauce Emulsion", editHtml);
        Assert.Contains("Always swirl diced cold butter", editHtml);

        // POST update
        var editToken = await Token(client, $"/contributions/tip/{itemId}/edit");
        var updateValues = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = editToken,
            ["Title"] = "Mastering Cold Butter Sauce Emulsions",
            ["Summary"] = "Updated comprehensive technique summary.",
            ["Body"] = "Cut unsalted butter into small cubes, chill thoroughly, and whisk vigorously into the pan reduction away from direct heat."
        };
        var editResponse = await client.PostAsync($"/contributions/tip/{itemId}/edit", new FormUrlEncodedContent(updateValues));
        Assert.Equal(HttpStatusCode.Redirect, editResponse.StatusCode);

        // Verify update in DB
        await using var verifyDb = fixture.CreateDb();
        var updatedItem = await verifyDb.ContentItems.Include(c => c.Tip).FirstAsync(c => c.Id == itemId);
        Assert.Equal("Mastering Cold Butter Sauce Emulsions", updatedItem.Title);
        Assert.Contains("Cut unsalted butter into small cubes", updatedItem.Tip!.Body);
        Assert.Equal(PublicationStatus.Pending, updatedItem.PublicationStatus);
    }

    [Fact]
    public async Task Contribution_Delete_SoftDeletes_RemovesFromActiveList()
    {
        var email = NewEmail();
        var password = NewPassword();
        using var client = fixture.NewClient();

        await RegisterAndLogin(client, email, password, "Delete Author");

        // Submit recipe
        var token = await Token(client, "/contributions/recipe/new");
        var values = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Title"] = "Temporary Recipe To Delete",
            ["Summary"] = "Will be deleted soon.",
            ["IngredientsText"] = "Item 1",
            ["StepsText"] = "Step 1: Preparation steps for recipe to be deleted."
        };
        await client.PostAsync("/contributions/recipe/new", new FormUrlEncodedContent(values));

        await using var db = fixture.CreateDb();
        var item = await db.ContentItems.FirstAsync(c => c.Title == "Temporary Recipe To Delete");
        var itemId = item.Id;

        // Author deletes own contribution
        var deleteToken = await Token(client, "/contributions");
        var deleteValues = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = deleteToken
        };
        var deleteResponse = await client.PostAsync($"/contributions/{itemId}/delete", new FormUrlEncodedContent(deleteValues));
        Assert.Equal(HttpStatusCode.Redirect, deleteResponse.StatusCode);

        // Verify soft-deleted in DB
        await using var verifyDb = fixture.CreateDb();
        var deletedItem = await verifyDb.ContentItems.FirstAsync(c => c.Id == itemId);
        Assert.NotNull(deletedItem.DeletedAtUtc);

        // Verify absent from active member list
        var indexHtml = await client.GetStringAsync("/contributions");
        Assert.DoesNotContain("Temporary Recipe To Delete", indexHtml);
    }
}
