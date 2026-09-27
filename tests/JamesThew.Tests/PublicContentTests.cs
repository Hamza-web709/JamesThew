using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using JamesThew.Authorization;
using JamesThew.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace JamesThew.Tests;

[Collection("Foundation")]
public class PublicContentTests(FoundationFixture fixture)
{
    private static string NewEmail() => Guid.NewGuid().ToString("N") + "@example.test";
    private static string NewPassword() => Convert.ToHexString(RandomNumberGenerator.GetBytes(20)) + "a!9";

    private static async Task<string> Token(HttpClient client, string path)
    {
        var html = await client.GetStringAsync(path);
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, "Expected an antiforgery token in the rendered form.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static async Task<HttpResponseMessage> RegisterAndLogin(HttpClient client, string email, string password)
    {
        var values = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await Token(client, "/account/register"),
            ["DisplayName"] = "Test Reader",
            ["Email"] = email,
            ["Password"] = password,
            ["ConfirmPassword"] = password
        };
        var registerResponse = await client.PostAsync("/account/register", new FormUrlEncodedContent(values));
        Assert.Equal(HttpStatusCode.Redirect, registerResponse.StatusCode);
        return registerResponse;
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

    [Fact]
    public async Task TC001_Guest_can_browse_home_and_reach_all_required_menu_pages()
    {
        using var client = fixture.NewClient();

        // 1. Home page renders owner story and sections
        var homeResponse = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, homeResponse.StatusCode);
        var homeHtml = await homeResponse.Content.ReadAsStringAsync();
        Assert.Contains("James Thew", homeHtml);
        Assert.Contains("Featured Cookery Recipes", homeHtml);
        Assert.Contains("Culinary Secrets", homeHtml);
        Assert.Contains("Frequently Asked Questions", homeHtml);

        // 2. All 7 required CRS points 1-2 menu pages + Login/Register are reachable (HTTP 200)
        var requiredRoutes = new[]
        {
            "/",
            "/recipes",
            "/tips",
            "/contests",
            "/announcements",
            "/feedback",
            "/faq",
            "/account/login",
            "/account/register"
        };

        foreach (var route in requiredRoutes)
        {
            var res = await client.GetAsync(route);
            Assert.True(res.StatusCode == HttpStatusCode.OK, $"Route {route} failed with status {res.StatusCode}");
        }
    }

    [Fact]
    public async Task TC020_Faq_page_renders_all_seven_required_questions_and_accessible_answers()
    {
        using var client = fixture.NewClient();
        var response = await client.GetAsync("/faq");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        // Check for all 7 required FAQ topics from PRD CRS 7 (p5-6)
        var requiredQuestionSnippets = new[]
        {
            "become a registered member",
            "subscription charges",
            "access recipes and cooking tips",
            "unregistered visitors participate in",
            "submit or upload their own recipes",
            "post feedback about a recipe",
            "contest winners selected and announced"
        };

        foreach (var snippet in requiredQuestionSnippets)
        {
            Assert.Contains(snippet, html, StringComparison.OrdinalIgnoreCase);
        }

        // Verify HTML uses semantic details/summary disclosures for no-JS accessibility
        Assert.Contains("<details", html);
        Assert.Contains("<summary", html);

        // Verify question key anchors exist
        Assert.Contains("id=\"faq-membership\"", html);
        Assert.Contains("id=\"faq-charges\"", html);
        Assert.Contains("id=\"faq-recipes-access\"", html);
        Assert.Contains("id=\"faq-contest-unregistered\"", html);
        Assert.Contains("id=\"faq-submit-content\"", html);
        Assert.Contains("id=\"faq-feedback\"", html);
        Assert.Contains("id=\"faq-winners\"", html);
    }

    [Fact]
    public async Task TC003_Guest_can_view_free_recipe_and_tip_details_without_authentication()
    {
        using var client = fixture.NewClient();

        // Free Recipe Detail
        var recipeResponse = await client.GetAsync("/recipes/jamess-classic-roast-herb-chicken");
        Assert.Equal(HttpStatusCode.OK, recipeResponse.StatusCode);
        var recipeHtml = await recipeResponse.Content.ReadAsStringAsync();

        Assert.Contains("James&#x27;s Classic Roast Herb Chicken", recipeHtml);
        Assert.Contains("Free Public Recipe", recipeHtml);
        Assert.Contains("Whole free-range chicken", recipeHtml);
        Assert.Contains("Unsalted butter", recipeHtml);
        Assert.Contains("Preheat oven to 200", recipeHtml);

        // Free Tip Detail
        var tipResponse = await client.GetAsync("/tips/mastering-chefs-knife-grip-and-precision-cuts");
        Assert.Equal(HttpStatusCode.OK, tipResponse.StatusCode);
        var tipHtml = await tipResponse.Content.ReadAsStringAsync();

        Assert.Contains("Mastering Chef&#x27;s Knife Grip", tipHtml);
        Assert.Contains("Free Cooking Tip", tipHtml);
        Assert.Contains("The Professional Pinch Grip", tipHtml);
        Assert.Contains("The Guiding &#x27;Claw&#x27;", tipHtml);
    }

    [Fact]
    public async Task TC003_Paid_recipe_and_tip_show_locked_preview_and_never_leak_protected_body_to_guests()
    {
        using var client = fixture.NewClient();

        // 1. Paid Recipe Detail for Guest
        var recipeResponse = await client.GetAsync("/recipes/jamess-masterclass-beef-wellington");
        Assert.Equal(HttpStatusCode.OK, recipeResponse.StatusCode);

        // Cache control check: must not cache protected items
        Assert.True(recipeResponse.Headers.CacheControl?.NoCache == true ||
                    recipeResponse.Headers.CacheControl?.NoStore == true);

        var recipeHtml = await recipeResponse.Content.ReadAsStringAsync();

        // Public teaser & locked box are visible
        Assert.Contains("James&#x27;s Masterclass Beef Wellington", recipeHtml);
        Assert.Contains("Exclusive Members-Only Recipe", recipeHtml);
        Assert.Contains("jt-locked-box", recipeHtml);
        Assert.Contains("$10 / mo", recipeHtml);
        Assert.Contains("$100 / yr", recipeHtml);
        Assert.Contains("Sign In to View", recipeHtml);

        // PROTECTED CONTENT MUST NEVER BE IN THE HTML
        Assert.DoesNotContain("English mustard", recipeHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("diamond lattice pattern", recipeHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("probe thermometer", recipeHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Chateaubriand", recipeHtml, StringComparison.OrdinalIgnoreCase);

        // 2. Paid Tip Detail for Guest
        var tipResponse = await client.GetAsync("/tips/masterclass-french-sauce-emulsions-and-pan-deglazing");
        Assert.Equal(HttpStatusCode.OK, tipResponse.StatusCode);

        Assert.True(tipResponse.Headers.CacheControl?.NoCache == true ||
                    tipResponse.Headers.CacheControl?.NoStore == true);

        var tipHtml = await tipResponse.Content.ReadAsStringAsync();

        Assert.Contains("Masterclass French Sauce Emulsions", tipHtml);
        Assert.Contains("Exclusive Masterclass Technique", tipHtml);
        Assert.Contains("jt-locked-box", tipHtml);
        Assert.Contains("$10 / mo", tipHtml);
        Assert.Contains("Sign In to View", tipHtml);

        // PROTECTED BODY MUST NEVER BE IN THE HTML
        Assert.DoesNotContain("Monter au Beurre", tipHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("nappe consistency", tipHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("deglaze the smoking pan", tipHtml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TC003_Search_does_not_leak_paid_content_via_secret_body_keywords()
    {
        using var client = fixture.NewClient();

        // 1. Search for a phrase that exists only in the paid recipe's hidden steps
        var secretRecipeSearch = await client.GetAsync("/recipes?q=diamond+lattice");
        Assert.Equal(HttpStatusCode.OK, secretRecipeSearch.StatusCode);
        var secretRecipeHtml = await secretRecipeSearch.Content.ReadAsStringAsync();
        Assert.Contains("No matching recipes found", secretRecipeHtml);
        Assert.DoesNotContain("Beef Wellington", secretRecipeHtml);

        // 2. Search for a phrase that exists only in the paid tip's hidden body
        var secretTipSearch = await client.GetAsync("/tips?q=Monter+au+Beurre");
        Assert.Equal(HttpStatusCode.OK, secretTipSearch.StatusCode);
        var secretTipHtml = await secretTipSearch.Content.ReadAsStringAsync();
        Assert.Contains("No matching tips found", secretTipHtml);
        Assert.DoesNotContain("French Sauce Emulsions", secretTipHtml);

        // 3. Search for public title of paid recipe returns teaser card
        var titleSearch = await client.GetAsync("/recipes?q=Wellington");
        Assert.Equal(HttpStatusCode.OK, titleSearch.StatusCode);
        var titleHtml = await titleSearch.Content.ReadAsStringAsync();
        Assert.Contains("Beef Wellington", titleHtml);
        Assert.Contains("Members-Only", titleHtml);

        // 4. Search for free recipe returns free item
        var freeSearch = await client.GetAsync("/recipes?q=sourdough");
        Assert.Equal(HttpStatusCode.OK, freeSearch.StatusCode);
        var freeHtml = await freeSearch.Content.ReadAsStringAsync();
        Assert.Contains("Sourdough Boule", freeHtml);
        Assert.Contains("Free Recipe", freeHtml);
    }

    [Fact]
    public async Task TC004_Ordinary_member_cannot_view_paid_content_prior_to_phase3_approval()
    {
        using var client = fixture.NewClient();
        var email = NewEmail(); var password = NewPassword();

        // Register and sign in as Member
        await RegisterAndLogin(client, email, password);

        // Even though authenticated, Member accounts DO NOT grant paid access in Phase 2
        var recipeResponse = await client.GetAsync("/recipes/jamess-masterclass-beef-wellington");
        Assert.Equal(HttpStatusCode.OK, recipeResponse.StatusCode);
        var recipeHtml = await recipeResponse.Content.ReadAsStringAsync();

        Assert.Contains("jt-locked-box", recipeHtml);
        Assert.DoesNotContain("English mustard", recipeHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("diamond lattice pattern", recipeHtml, StringComparison.OrdinalIgnoreCase);

        var tipResponse = await client.GetAsync("/tips/masterclass-french-sauce-emulsions-and-pan-deglazing");
        Assert.Equal(HttpStatusCode.OK, tipResponse.StatusCode);
        var tipHtml = await tipResponse.Content.ReadAsStringAsync();

        Assert.Contains("jt-locked-box", tipHtml);
        Assert.DoesNotContain("Monter au Beurre", tipHtml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TC004_Admin_can_view_paid_recipe_and_tip_details()
    {
        var email = NewEmail(); var password = NewPassword();

        await using var scope = fixture.Services.CreateAsyncScope();
        await IdentitySeeder.SeedLocalAdminAsync(
            scope.ServiceProvider,
            SeedConfig(email, password),
            new TestEnvironment("Development"));

        using var client = fixture.NewClient();
        await Login(client, email, password);

        // Admin can view paid recipe details without locked box
        var recipeResponse = await client.GetAsync("/recipes/jamess-masterclass-beef-wellington");
        Assert.Equal(HttpStatusCode.OK, recipeResponse.StatusCode);
        var recipeHtml = await recipeResponse.Content.ReadAsStringAsync();

        Assert.DoesNotContain("jt-locked-box", recipeHtml);
        Assert.Contains("English mustard", recipeHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("diamond lattice pattern", recipeHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Chateaubriand", recipeHtml, StringComparison.OrdinalIgnoreCase);

        // Admin can view paid tip details without locked box
        var tipResponse = await client.GetAsync("/tips/masterclass-french-sauce-emulsions-and-pan-deglazing");
        Assert.Equal(HttpStatusCode.OK, tipResponse.StatusCode);
        var tipHtml = await tipResponse.Content.ReadAsStringAsync();

        Assert.DoesNotContain("jt-locked-box", tipHtml);
        Assert.Contains("Monter au Beurre", tipHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("nappe consistency", tipHtml, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "JamesThew.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
