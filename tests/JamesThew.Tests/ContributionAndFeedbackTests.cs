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
public class ContributionAndFeedbackTests(FoundationFixture fixture)
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

    private static async Task RegisterAndLogin(HttpClient client, string email, string password, string displayName = "Test Member") =>
        await TestAuth.RegisterAndLogin(client, email, password, displayName);

    private static async Task Login(HttpClient client, string email, string password) =>
        await TestAuth.Login(client, email, password);

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

    [Fact]
    public async Task Guest_cannot_submit_feedback_and_is_redirected_to_login()
    {
        using var client = fixture.NewClient();
        var token = await Token(client, "/account/login");

        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Form.Category"] = "General Culinary Feedback",
            ["Form.Message"] = "Guest feedback attempt that should be blocked by authorization."
        };

        var response = await client.PostAsync("/feedback/submit", new FormUrlEncodedContent(form));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/account/login", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Member_can_submit_feedback_and_feedback_is_not_public()
    {
        using var memberClient = fixture.NewClient();
        var memberEmail = NewEmail();
        var memberPassword = NewPassword();
        await RegisterAndLogin(memberClient, memberEmail, memberPassword, "Culinary Contributor");

        var token = await Token(memberClient, "/feedback");
        var secretFeedbackMessage = $"Special private feedback note {Guid.NewGuid():N}: Love the classic roast chicken technique!";

        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Form.Category"] = "Recipe Technique Question",
            ["Form.Rating"] = "5",
            ["Form.Message"] = secretFeedbackMessage
        };

        var postResponse = await memberClient.PostAsync("/feedback/submit", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);

        // 1. Verify saved in database with Pending status
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == memberEmail);
            var fb = await db.Feedbacks.FirstOrDefaultAsync(f => f.AuthorUserId == user.Id && f.Message == secretFeedbackMessage);
            Assert.NotNull(fb);
            Assert.Equal(FeedbackStatus.Pending, fb.Status);
            Assert.Equal(5, fb.Rating);
        }

        // 2. Member's own feedback page shows it in their personal history
        var memberHtml = await memberClient.GetStringAsync("/feedback");
        Assert.Contains(secretFeedbackMessage, memberHtml);
        Assert.Contains("Pending Review", memberHtml);

        // 3. Guest visiting /feedback must NOT see the private feedback message
        using var guestClient = fixture.NewClient();
        var guestHtml = await guestClient.GetStringAsync("/feedback");
        Assert.DoesNotContain(secretFeedbackMessage, guestHtml);
        Assert.Contains("Sign-In Required to Submit Feedback", guestHtml);
    }

    [Fact]
    public async Task Admin_can_view_submitted_feedback_and_member_is_forbidden()
    {
        // 1. Member submits feedback
        using var memberClient = fixture.NewClient();
        var memberEmail = NewEmail();
        var memberPassword = NewPassword();
        await RegisterAndLogin(memberClient, memberEmail, memberPassword, "Review Member");

        var token = await Token(memberClient, "/feedback");
        var uniqueNote = $"Admin inspection test feedback {Guid.NewGuid():N}";
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Form.Category"] = "Masterclass Suggestion",
            ["Form.Rating"] = "4",
            ["Form.Message"] = uniqueNote
        };
        await memberClient.PostAsync("/feedback/submit", new FormUrlEncodedContent(form));

        // 2. Member tries to access admin feedback page -> AccessDenied / Forbidden
        var forbiddenResponse = await memberClient.GetAsync("/admin/feedback");
        Assert.True(forbiddenResponse.StatusCode == HttpStatusCode.Forbidden ||
            (forbiddenResponse.StatusCode == HttpStatusCode.Redirect &&
             forbiddenResponse.Headers.Location?.OriginalString.Contains("access-denied", StringComparison.OrdinalIgnoreCase) == true));

        // 3. Admin logs in and views /admin/feedback
        using var adminClient = fixture.NewClient();
        var adminEmail = NewEmail();
        var adminPassword = NewPassword();
        using (var scope = fixture.Services.CreateScope())
        {
            var env = new DevEnv();
            var config = SeedConfig(adminEmail, adminPassword);
            await IdentitySeeder.SeedLocalAdminAsync(scope.ServiceProvider, config, env);
        }
        await Login(adminClient, adminEmail, adminPassword);

        var adminHtml = await adminClient.GetStringAsync("/admin/feedback");
        Assert.Contains(uniqueNote, adminHtml);
        Assert.Contains(memberEmail, adminHtml);
        Assert.Contains("Masterclass Suggestion", adminHtml);
    }

    [Fact]
    public async Task Guest_cannot_submit_recipe_contribution_and_is_redirected_to_login()
    {
        using var client = fixture.NewClient();
        var token = await Token(client, "/account/login");

        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Title"] = "Guest Recipe Attempt",
            ["Summary"] = "This recipe should be blocked by authentication.",
            ["IngredientsText"] = "1 cup salt",
            ["StepsText"] = "Mix and bake"
        };

        var response = await client.PostAsync("/contributions/recipe/new", new FormUrlEncodedContent(form));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/account/login", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Member_can_submit_recipe_contribution_and_it_remains_pending()
    {
        using var memberClient = fixture.NewClient();
        var memberEmail = NewEmail();
        var memberPassword = NewPassword();
        await RegisterAndLogin(memberClient, memberEmail, memberPassword, "Chef Artisan");

        var token = await Token(memberClient, "/contributions/recipe/new");
        var recipeTitle = $"Rustic Sourdough Focaccia {Guid.NewGuid():N}";
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Title"] = recipeTitle,
            ["Summary"] = "A heritage sourdough focaccia with fresh rosemary, flaky sea salt, and extra virgin olive oil.",
            ["Servings"] = "6",
            ["PrepMinutes"] = "30",
            ["CookMinutes"] = "25",
            ["IngredientsText"] = "500g strong bread flour\n350ml warm water\n100g active sourdough starter\n10g fine sea salt\n3 tbsp extra virgin olive oil",
            ["StepsText"] = "Mix flour, water, and starter.\nKnead gently and rest for 4 hours.\nDimple dough with olive oil and bake at 220C for 25 minutes.",
            ["Notes"] = "Use cold fermentation overnight for deeper flavor."
        };

        var postResponse = await memberClient.PostAsync("/contributions/recipe/new", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);
        Assert.Equal("/contributions", postResponse.Headers.Location?.OriginalString);

        // Verify in database
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var item = await db.ContentItems
                .Include(c => c.Recipe)
                    .ThenInclude(r => r!.Ingredients)
                .Include(c => c.Recipe)
                    .ThenInclude(r => r!.Steps)
                .FirstOrDefaultAsync(c => c.Title == recipeTitle);

            Assert.NotNull(item);
            Assert.Equal(ContentKind.Recipe, item.Kind);
            Assert.Equal(ContentOrigin.Community, item.Origin);
            Assert.Equal(PublicationStatus.Pending, item.PublicationStatus);
            Assert.Equal(5, item.Recipe?.Ingredients.Count);
            Assert.Equal(3, item.Recipe?.Steps.Count);
            Assert.Equal("Use cold fermentation overnight for deeper flavor.", item.ContributorNotes);
        }

        // Verify member's contributions page
        var memberHtml = await memberClient.GetStringAsync("/contributions");
        Assert.Contains(recipeTitle, memberHtml);
        Assert.Contains("Pending Review", memberHtml);
    }

    [Fact]
    public async Task Pending_recipe_contribution_does_not_appear_publicly()
    {
        // 1. Submit a pending recipe
        using var memberClient = fixture.NewClient();
        var memberEmail = NewEmail();
        var memberPassword = NewPassword();
        await RegisterAndLogin(memberClient, memberEmail, memberPassword, "Secret Baker");

        var token = await Token(memberClient, "/contributions/recipe/new");
        var secretRecipeTitle = $"Classified Truffle Risotto {Guid.NewGuid():N}";
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Title"] = secretRecipeTitle,
            ["Summary"] = "An exclusive unapproved member submission that must stay private.",
            ["IngredientsText"] = "300g Carnaroli rice\n50g Black summer truffle",
            ["StepsText"] = "Toast rice in butter.\nSimmer slowly with stock."
        };
        await memberClient.PostAsync("/contributions/recipe/new", new FormUrlEncodedContent(form));

        string slug;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var item = await db.ContentItems.FirstAsync(c => c.Title == secretRecipeTitle);
            slug = item.Slug;
        }

        // 2. Check public catalog (/recipes)
        using var guestClient = fixture.NewClient();
        var recipesHtml = await guestClient.GetStringAsync("/recipes");
        Assert.DoesNotContain(secretRecipeTitle, recipesHtml);

        // 3. Check public search (/recipes?q=...)
        var searchHtml = await guestClient.GetStringAsync($"/recipes?q={WebUtility.UrlEncode(secretRecipeTitle)}");
        Assert.DoesNotContain("jt-card", searchHtml);
        Assert.Contains("No matching recipes found", searchHtml);

        // 4. Check Home page featured section
        var homeHtml = await guestClient.GetStringAsync("/");
        Assert.DoesNotContain(secretRecipeTitle, homeHtml);

        // 5. Check direct detail slug (/recipes/{slug}) -> 404
        var detailResponse = await guestClient.GetAsync($"/recipes/{slug}");
        Assert.Equal(HttpStatusCode.NotFound, detailResponse.StatusCode);
    }

    [Fact]
    public async Task Guest_cannot_submit_tip_contribution_and_is_redirected_to_login()
    {
        using var client = fixture.NewClient();
        var token = await Token(client, "/account/login");

        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Title"] = "Guest Tip Attempt",
            ["Summary"] = "This tip should be blocked by authentication.",
            ["Body"] = "Full tip text that should never be saved anonymously."
        };

        var response = await client.PostAsync("/contributions/tip/new", new FormUrlEncodedContent(form));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/account/login", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Member_can_submit_tip_contribution_and_it_remains_pending()
    {
        using var memberClient = fixture.NewClient();
        var memberEmail = NewEmail();
        var memberPassword = NewPassword();
        await RegisterAndLogin(memberClient, memberEmail, memberPassword, "Tip Master");

        var token = await Token(memberClient, "/contributions/tip/new");
        var tipTitle = $"Sharpening Whetstone Mastery {Guid.NewGuid():N}";
        var tipBody = "Always submerge 1000-grit water stones in cold water for 15 minutes until bubbles cease. Maintain a strict 15-degree bevel angle along the blade curve.";

        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Title"] = tipTitle,
            ["Summary"] = "How to properly prep and angle a water stone for razor-sharp Japanese chef knives.",
            ["Body"] = tipBody,
            ["Notes"] = "Knife Maintenance & Prep"
        };

        var postResponse = await memberClient.PostAsync("/contributions/tip/new", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);
        Assert.Equal("/contributions", postResponse.Headers.Location?.OriginalString);

        // Verify in database
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var item = await db.ContentItems
                .Include(c => c.Tip)
                .FirstOrDefaultAsync(c => c.Title == tipTitle);

            Assert.NotNull(item);
            Assert.Equal(ContentKind.Tip, item.Kind);
            Assert.Equal(ContentOrigin.Community, item.Origin);
            Assert.Equal(PublicationStatus.Pending, item.PublicationStatus);
            Assert.Equal(tipBody, item.Tip?.Body);
        }

        // Verify member's contributions page
        var memberHtml = await memberClient.GetStringAsync("/contributions");
        Assert.Contains(tipTitle, memberHtml);
        Assert.Contains("Pending Review", memberHtml);
    }

    [Fact]
    public async Task Pending_tip_contribution_does_not_appear_publicly()
    {
        // 1. Submit pending tip
        using var memberClient = fixture.NewClient();
        var memberEmail = NewEmail();
        var memberPassword = NewPassword();
        await RegisterAndLogin(memberClient, memberEmail, memberPassword, "Secret Chef");

        var token = await Token(memberClient, "/contributions/tip/new");
        var secretTipTitle = $"Classified Emulsion Stability {Guid.NewGuid():N}";
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Title"] = secretTipTitle,
            ["Summary"] = "A secret kitchen technique that should not be visible publicly.",
            ["Body"] = "Never allow vinegar reduction to boil above 85C when whisking egg yolks."
        };
        await memberClient.PostAsync("/contributions/tip/new", new FormUrlEncodedContent(form));

        string slug;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var item = await db.ContentItems.FirstAsync(c => c.Title == secretTipTitle);
            slug = item.Slug;
        }

        // 2. Check public /tips catalog
        using var guestClient = fixture.NewClient();
        var tipsHtml = await guestClient.GetStringAsync("/tips");
        Assert.DoesNotContain(secretTipTitle, tipsHtml);

        // 3. Check public search
        var searchHtml = await guestClient.GetStringAsync($"/tips?q={WebUtility.UrlEncode(secretTipTitle)}");
        Assert.DoesNotContain("jt-card", searchHtml);
        Assert.Contains("No matching tips found", searchHtml);

        // 4. Check direct detail slug (/tips/{slug}) -> 404
        var detailResponse = await guestClient.GetAsync($"/tips/{slug}");
        Assert.Equal(HttpStatusCode.NotFound, detailResponse.StatusCode);
    }

    [Fact]
    public async Task Admin_can_view_pending_contributions_intake_and_member_is_forbidden()
    {
        // 1. Member submits a recipe
        using var memberClient = fixture.NewClient();
        var memberEmail = NewEmail();
        var memberPassword = NewPassword();
        await RegisterAndLogin(memberClient, memberEmail, memberPassword, "Intake Contributor");

        var token = await Token(memberClient, "/contributions/recipe/new");
        var recipeTitle = $"Admin Intake Test Recipe {Guid.NewGuid():N}";
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Title"] = recipeTitle,
            ["Summary"] = "Testing admin contribution intake review screen.",
            ["IngredientsText"] = "200g wild mushrooms\n2 garlic cloves",
            ["StepsText"] = "Saute mushrooms until brown."
        };
        await memberClient.PostAsync("/contributions/recipe/new", new FormUrlEncodedContent(form));

        // 2. Member tries to access /admin/contributions -> AccessDenied / Forbidden
        var forbiddenResponse = await memberClient.GetAsync("/admin/contributions");
        Assert.True(forbiddenResponse.StatusCode == HttpStatusCode.Forbidden ||
            (forbiddenResponse.StatusCode == HttpStatusCode.Redirect &&
             forbiddenResponse.Headers.Location?.OriginalString.Contains("access-denied", StringComparison.OrdinalIgnoreCase) == true));

        // 3. Admin views /admin/contributions
        using var adminClient = fixture.NewClient();
        var adminEmail = NewEmail();
        var adminPassword = NewPassword();
        using (var scope = fixture.Services.CreateScope())
        {
            var env = new DevEnv();
            var config = SeedConfig(adminEmail, adminPassword);
            await IdentitySeeder.SeedLocalAdminAsync(scope.ServiceProvider, config, env);
        }
        await Login(adminClient, adminEmail, adminPassword);

        var adminHtml = await adminClient.GetStringAsync("/admin/contributions");
        Assert.Contains(recipeTitle, adminHtml);
        Assert.Contains(memberEmail, adminHtml);
        Assert.Contains("wild mushrooms", adminHtml);
    }
}
