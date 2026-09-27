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
public class ModerationTests(FoundationFixture fixture)
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
    public async Task Guest_cannot_moderate_feedback_or_contributions_and_is_redirected_to_login()
    {
        using var client = fixture.NewClient();
        var token = await Token(client, "/account/login");

        var fbValues = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["status"] = "Reviewed",
            ["adminNotes"] = "Unauthorized test"
        };
        var fbResponse = await client.PostAsync("/admin/feedback/1/moderate", new FormUrlEncodedContent(fbValues));
        Assert.Equal(HttpStatusCode.Redirect, fbResponse.StatusCode);
        Assert.Contains("/account/login", fbResponse.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);

        var appValues = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["visibility"] = "Free"
        };
        var appResponse = await client.PostAsync("/admin/contributions/1/approve", new FormUrlEncodedContent(appValues));
        Assert.Equal(HttpStatusCode.Redirect, appResponse.StatusCode);
        Assert.Contains("/account/login", appResponse.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);

        var rejValues = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["rejectionReason"] = "Unauthorized test"
        };
        var rejResponse = await client.PostAsync("/admin/contributions/1/reject", new FormUrlEncodedContent(rejValues));
        Assert.Equal(HttpStatusCode.Redirect, rejResponse.StatusCode);
        Assert.Contains("/account/login", rejResponse.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Member_cannot_moderate_feedback_or_contributions_and_receives_access_denied()
    {
        using var client = fixture.NewClient();
        var memberEmail = NewEmail();
        var memberPassword = NewPassword();
        await RegisterAndLogin(client, memberEmail, memberPassword, "Regular Foodie");

        var token = await Token(client, "/feedback");

        var fbValues = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["status"] = "Reviewed"
        };
        var fbResponse = await client.PostAsync("/admin/feedback/1/moderate", new FormUrlEncodedContent(fbValues));
        Assert.Equal(HttpStatusCode.Redirect, fbResponse.StatusCode);
        Assert.Contains("access-denied", fbResponse.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);

        var appValues = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["visibility"] = "Free"
        };
        var appResponse = await client.PostAsync("/admin/contributions/1/approve", new FormUrlEncodedContent(appValues));
        Assert.Equal(HttpStatusCode.Redirect, appResponse.StatusCode);
        Assert.Contains("access-denied", appResponse.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);

        var rejValues = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["rejectionReason"] = "Self reject attempt"
        };
        var rejResponse = await client.PostAsync("/admin/contributions/1/reject", new FormUrlEncodedContent(rejValues));
        Assert.Equal(HttpStatusCode.Redirect, rejResponse.StatusCode);
        Assert.Contains("access-denied", rejResponse.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Admin_can_moderate_feedback_status_and_notes_and_member_can_view_note_privately()
    {
        // 1. Member submits feedback
        using var memberClient = fixture.NewClient();
        var memberEmail = NewEmail();
        var memberPassword = NewPassword();
        await RegisterAndLogin(memberClient, memberEmail, memberPassword, "Feedback Contributor");

        var feedbackMessage = $"Phase 3C Feedback Inquiry {Guid.NewGuid():N}: Please provide more pastry techniques!";
        var memToken = await Token(memberClient, "/feedback");
        var fbSubmitForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = memToken,
            ["Form.Category"] = "Recipe Suggestion",
            ["Form.Rating"] = "5",
            ["Form.Message"] = feedbackMessage
        };
        var submitResp = await memberClient.PostAsync("/feedback/submit", new FormUrlEncodedContent(fbSubmitForm));
        Assert.Equal(HttpStatusCode.Redirect, submitResp.StatusCode);

        int feedbackId;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var fb = await db.Feedbacks.FirstAsync(f => f.Message == feedbackMessage);
            feedbackId = fb.Id;
            Assert.Equal(FeedbackStatus.Pending, fb.Status);
        }

        // 2. Admin logs in and marks feedback as Reviewed with an Admin note
        using var adminClient = await CreateAdminClientAsync();
        var adminToken = await Token(adminClient, "/admin/feedback");

        var adminNote = "Thank you! Chef James is scheduling a choux pastry masterclass.";
        var modForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = adminToken,
            ["status"] = FeedbackStatus.Reviewed.ToString(),
            ["adminNotes"] = adminNote
        };
        var modResp = await adminClient.PostAsync($"/admin/feedback/{feedbackId}/moderate", new FormUrlEncodedContent(modForm));
        Assert.Equal(HttpStatusCode.Redirect, modResp.StatusCode);

        // Verify database state
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var fb = await db.Feedbacks.FirstAsync(f => f.Id == feedbackId);
            Assert.Equal(FeedbackStatus.Reviewed, fb.Status);
            Assert.Equal(adminNote, fb.AdminNotes);
        }

        // 3. Member views their history and sees Reviewed status and the Admin Note
        var memberHtml = await memberClient.GetStringAsync("/feedback");
        Assert.Contains(feedbackMessage, memberHtml);
        Assert.Contains("Reviewed", memberHtml);
        Assert.Contains(adminNote, memberHtml);

        // 4. Guest visiting /feedback does NOT see the member's private feedback
        using var guestClient = fixture.NewClient();
        var guestHtml = await guestClient.GetStringAsync("/feedback");
        Assert.DoesNotContain(feedbackMessage, guestHtml);
        Assert.DoesNotContain(adminNote, guestHtml);
    }

    [Fact]
    public async Task Admin_feedback_moderation_handles_missing_id_and_repeated_status_gracefully()
    {
        using var adminClient = await CreateAdminClientAsync();
        var adminToken = await Token(adminClient, "/admin/feedback");

        // Non-existent ID
        var modForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = adminToken,
            ["status"] = "Reviewed",
            ["adminNotes"] = "Ghost feedback"
        };
        var resp1 = await adminClient.PostAsync("/admin/feedback/999999/moderate", new FormUrlEncodedContent(modForm));
        Assert.Equal(HttpStatusCode.Redirect, resp1.StatusCode);

        var listHtml = await adminClient.GetStringAsync(resp1.Headers.Location?.OriginalString ?? "/admin/feedback");
        Assert.Contains("was not found", listHtml);
    }

    [Fact]
    public async Task Admin_can_approve_recipe_contribution_and_it_becomes_publicly_visible_as_free()
    {
        // 1. Member submits a recipe
        using var memberClient = fixture.NewClient();
        var memberEmail = NewEmail();
        var memberPassword = NewPassword();
        await RegisterAndLogin(memberClient, memberEmail, memberPassword, "Baker Alice");

        var recipeTitle = $"Artisanal Focaccia {Guid.NewGuid():N}";
        var memToken = await Token(memberClient, "/contributions/recipe/new");
        var recipeForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = memToken,
            ["Title"] = recipeTitle,
            ["Summary"] = "Crispy golden rosemary sea salt focaccia with olive oil dimples.",
            ["Servings"] = "8",
            ["PrepMinutes"] = "30",
            ["CookMinutes"] = "25",
            ["IngredientsText"] = "500g strong bread flour\n380ml warm water\n7g instant yeast\n10g fine sea salt\n50ml extra virgin olive oil\nFresh rosemary sprigs",
            ["StepsText"] = "Whisk yeast and warm water until frothy.\nMix in flour and salt to form high hydration dough.\nPerform coil folds every 30 minutes.\nDimple dough with fingertips and top with olive oil and rosemary.\nBake at 220C for 25 minutes until golden.",
            ["Notes"] = "Best baked on a preheated pizza stone."
        };

        var submitResp = await memberClient.PostAsync("/contributions/recipe/new", new FormUrlEncodedContent(recipeForm));
        Assert.Equal(HttpStatusCode.Redirect, submitResp.StatusCode);

        int itemId;
        string itemSlug;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var item = await db.ContentItems.FirstAsync(c => c.Title == recipeTitle);
            itemId = item.Id;
            itemSlug = item.Slug;
            Assert.Equal(PublicationStatus.Pending, item.PublicationStatus);
            Assert.Equal(ContentOrigin.Community, item.Origin);
        }

        // Before approval: guest gets 404 and catalog does not have it
        using var guestClient = fixture.NewClient();
        var preCatalog = await guestClient.GetStringAsync("/recipes");
        Assert.DoesNotContain(recipeTitle, preCatalog);

        var preDetail = await guestClient.GetAsync($"/recipes/{itemSlug}");
        Assert.Equal(HttpStatusCode.NotFound, preDetail.StatusCode);

        // 2. Admin approves and publishes the recipe
        using var adminClient = await CreateAdminClientAsync();
        var adminToken = await Token(adminClient, "/admin/contributions");
        var approveForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = adminToken,
            ["visibility"] = ContentVisibility.Free.ToString()
        };
        var approveResp = await adminClient.PostAsync($"/admin/contributions/{itemId}/approve", new FormUrlEncodedContent(approveForm));
        Assert.Equal(HttpStatusCode.Redirect, approveResp.StatusCode);

        // Verify DB update
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var item = await db.ContentItems.FirstAsync(c => c.Id == itemId);
            Assert.Equal(PublicationStatus.Published, item.PublicationStatus);
            Assert.Equal(ContentVisibility.Free, item.Visibility);
            Assert.NotNull(item.UpdatedAtUtc);
            Assert.Null(item.RejectionReason);
        }

        // 3. Guest visits /recipes: title now appears in public catalog
        var postCatalog = await guestClient.GetStringAsync("/recipes");
        Assert.Contains(recipeTitle, postCatalog);

        // 4. Guest visits /recipes/{slug}: 200 OK and full ingredients/steps are visible (Free)
        var postDetail = await guestClient.GetAsync($"/recipes/{itemSlug}");
        Assert.Equal(HttpStatusCode.OK, postDetail.StatusCode);
        var detailHtml = await postDetail.Content.ReadAsStringAsync();
        Assert.Contains(recipeTitle, detailHtml);
        Assert.Contains("500g strong bread flour", detailHtml);
        Assert.Contains("Dimple dough with fingertips", detailHtml);
        Assert.DoesNotContain("jt-locked-box", detailHtml);

        // 5. Member dashboard shows Published status and live link
        var memberDashboard = await memberClient.GetStringAsync("/contributions");
        Assert.Contains(recipeTitle, memberDashboard);
        Assert.Contains("Published", memberDashboard);
        Assert.Contains($"/recipes/{itemSlug}", memberDashboard);
    }

    [Fact]
    public async Task Admin_can_reject_recipe_contribution_and_it_remains_hidden_with_rejection_reason_shown_to_member()
    {
        // 1. Member submits recipe
        using var memberClient = fixture.NewClient();
        var memberEmail = NewEmail();
        var memberPassword = NewPassword();
        await RegisterAndLogin(memberClient, memberEmail, memberPassword, "Novice Cook");

        var recipeTitle = $"Quick Microwave Cake {Guid.NewGuid():N}";
        var memToken = await Token(memberClient, "/contributions/recipe/new");
        var recipeForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = memToken,
            ["Title"] = recipeTitle,
            ["Summary"] = "Fast mug cake made in 2 minutes.",
            ["IngredientsText"] = "Flour\nSugar\nCocoa",
            ["StepsText"] = "Mix everything in mug.\nMicrowave on high."
        };
        await memberClient.PostAsync("/contributions/recipe/new", new FormUrlEncodedContent(recipeForm));

        int itemId;
        string itemSlug;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var item = await db.ContentItems.FirstAsync(c => c.Title == recipeTitle);
            itemId = item.Id;
            itemSlug = item.Slug;
        }

        // 2. Admin rejects the recipe with a descriptive reason
        using var adminClient = await CreateAdminClientAsync();
        var adminToken = await Token(adminClient, "/admin/contributions");
        var rejectionReason = "Please provide exact ingredient weights and oven temperatures rather than microwave instructions.";
        var rejectForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = adminToken,
            ["rejectionReason"] = rejectionReason
        };
        var rejectResp = await adminClient.PostAsync($"/admin/contributions/{itemId}/reject", new FormUrlEncodedContent(rejectForm));
        Assert.Equal(HttpStatusCode.Redirect, rejectResp.StatusCode);

        // Verify DB update
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var item = await db.ContentItems.FirstAsync(c => c.Id == itemId);
            Assert.Equal(PublicationStatus.Rejected, item.PublicationStatus);
            Assert.Equal(rejectionReason, item.RejectionReason);
            Assert.NotNull(item.UpdatedAtUtc);
        }

        // 3. Guest cannot view rejected item
        using var guestClient = fixture.NewClient();
        var catalog = await guestClient.GetStringAsync("/recipes");
        Assert.DoesNotContain(recipeTitle, catalog);

        var detailResp = await guestClient.GetAsync($"/recipes/{itemSlug}");
        Assert.Equal(HttpStatusCode.NotFound, detailResp.StatusCode);

        // 4. Member views their contributions dashboard and sees Rejected status with the reason
        var memberHtml = await memberClient.GetStringAsync("/contributions");
        Assert.Contains(recipeTitle, memberHtml);
        Assert.Contains("Rejected", memberHtml);
        Assert.Contains(rejectionReason, memberHtml);
    }

    [Fact]
    public async Task Admin_can_approve_tip_contribution_and_it_becomes_publicly_visible()
    {
        // 1. Member submits tip
        using var memberClient = fixture.NewClient();
        var memberEmail = NewEmail();
        var memberPassword = NewPassword();
        await RegisterAndLogin(memberClient, memberEmail, memberPassword, "Seasoned Chef");

        var tipTitle = $"Flaky Pastry Butter Temperature {Guid.NewGuid():N}";
        var memToken = await Token(memberClient, "/contributions/tip/new");
        var tipForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = memToken,
            ["Title"] = tipTitle,
            ["Summary"] = "How keeping unsalted European butter cold yields distinct lamination layers.",
            ["Body"] = "Always grate freezing cold butter directly into seasoned flour and keep the water at 2 degrees Celsius to prevent butter melting prior to baking."
        };
        await memberClient.PostAsync("/contributions/tip/new", new FormUrlEncodedContent(tipForm));

        int itemId;
        string itemSlug;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var item = await db.ContentItems.FirstAsync(c => c.Title == tipTitle);
            itemId = item.Id;
            itemSlug = item.Slug;
        }

        // 2. Admin approves tip
        using var adminClient = await CreateAdminClientAsync();
        var adminToken = await Token(adminClient, "/admin/contributions");
        var approveForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = adminToken,
            ["visibility"] = ContentVisibility.Free.ToString()
        };
        await adminClient.PostAsync($"/admin/contributions/{itemId}/approve", new FormUrlEncodedContent(approveForm));

        // 3. Guest visits /tips: tip appears
        using var guestClient = fixture.NewClient();
        var tipsCatalog = await guestClient.GetStringAsync("/tips");
        Assert.Contains(tipTitle, tipsCatalog);

        // 4. Guest visits /tips/{slug}: 200 OK and full body rendered
        var tipDetailResp = await guestClient.GetAsync($"/tips/{itemSlug}");
        Assert.Equal(HttpStatusCode.OK, tipDetailResp.StatusCode);
        var tipHtml = await tipDetailResp.Content.ReadAsStringAsync();
        Assert.Contains(tipTitle, tipHtml);
        Assert.Contains("grate freezing cold butter", tipHtml);
    }

    [Fact]
    public async Task Admin_can_reject_tip_contribution_and_it_remains_hidden()
    {
        using var memberClient = fixture.NewClient();
        var memberEmail = NewEmail();
        var memberPassword = NewPassword();
        await RegisterAndLogin(memberClient, memberEmail, memberPassword, "Experimental Baker");

        var tipTitle = $"Dubious Garlic Preservation {Guid.NewGuid():N}";
        var memToken = await Token(memberClient, "/contributions/tip/new");
        var tipForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = memToken,
            ["Title"] = tipTitle,
            ["Summary"] = "Room temperature raw garlic stored in oil.",
            ["Body"] = "Store raw minced garlic in olive oil on the kitchen counter for months."
        };
        await memberClient.PostAsync("/contributions/tip/new", new FormUrlEncodedContent(tipForm));

        int itemId;
        string itemSlug;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var item = await db.ContentItems.FirstAsync(c => c.Title == tipTitle);
            itemId = item.Id;
            itemSlug = item.Slug;
        }

        using var adminClient = await CreateAdminClientAsync();
        var adminToken = await Token(adminClient, "/admin/contributions");
        var rejectForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = adminToken,
            ["rejectionReason"] = "Violates food safety protocols regarding botulism risks in untreated room temperature garlic oil."
        };
        await adminClient.PostAsync($"/admin/contributions/{itemId}/reject", new FormUrlEncodedContent(rejectForm));

        // Ensure not found publicly
        using var guestClient = fixture.NewClient();
        var tipsCatalog = await guestClient.GetStringAsync("/tips");
        Assert.DoesNotContain(tipTitle, tipsCatalog);

        var tipDetail = await guestClient.GetAsync($"/tips/{itemSlug}");
        Assert.Equal(HttpStatusCode.NotFound, tipDetail.StatusCode);
    }

    [Fact]
    public async Task Admin_contribution_moderation_handles_missing_item_and_repeated_approvals_gracefully()
    {
        using var adminClient = await CreateAdminClientAsync();
        var adminToken = await Token(adminClient, "/admin/contributions");

        // Missing ID
        var approveForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = adminToken,
            ["visibility"] = "Free"
        };
        var resp1 = await adminClient.PostAsync("/admin/contributions/999999/approve", new FormUrlEncodedContent(approveForm));
        Assert.Equal(HttpStatusCode.Redirect, resp1.StatusCode);
        var html1 = await adminClient.GetStringAsync(resp1.Headers.Location?.OriginalString ?? "/admin/contributions");
        Assert.Contains("was not found", html1);
    }

    [Fact]
    public async Task Approved_contribution_with_MembersOnly_visibility_protects_body_for_guests_and_unlocks_for_approved_members()
    {
        // 1. Member submits recipe
        using var memberClient = fixture.NewClient();
        var memberEmail = NewEmail();
        var memberPassword = NewPassword();
        await RegisterAndLogin(memberClient, memberEmail, memberPassword, "Masterclass Submitter");

        var recipeTitle = $"Exclusive Sous Vide Duck Breast {Guid.NewGuid():N}";
        var memToken = await Token(memberClient, "/contributions/recipe/new");
        var recipeForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = memToken,
            ["Title"] = recipeTitle,
            ["Summary"] = "Masterclass duck breast with crispy cross-hatch scored skin.",
            ["Servings"] = "2",
            ["IngredientsText"] = "2 Moulard duck breasts\nCoarse Maldon sea salt\nBlack peppercorns\nFresh thyme sprigs",
            ["StepsText"] = "Score skin in tight diamond pattern.\nCook sous vide at 57C for 2 hours.\nSear cold pan skin side down to render fat crisp."
        };
        await memberClient.PostAsync("/contributions/recipe/new", new FormUrlEncodedContent(recipeForm));

        int itemId;
        string itemSlug;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var item = await db.ContentItems.FirstAsync(c => c.Title == recipeTitle);
            itemId = item.Id;
            itemSlug = item.Slug;
        }

        // 2. Admin approves with MembersOnly visibility
        using var adminClient = await CreateAdminClientAsync();
        var adminToken = await Token(adminClient, "/admin/contributions");
        var approveForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = adminToken,
            ["visibility"] = ContentVisibility.MembersOnly.ToString()
        };
        await adminClient.PostAsync($"/admin/contributions/{itemId}/approve", new FormUrlEncodedContent(approveForm));

        // 3. Guest views detail: locked box rendered, ingredients and steps withheld, no-cache header set
        using var guestClient = fixture.NewClient();
        var guestDetailResp = await guestClient.GetAsync($"/recipes/{itemSlug}");
        Assert.Equal(HttpStatusCode.OK, guestDetailResp.StatusCode);
        var guestCache = guestDetailResp.Headers.CacheControl?.ToString() ?? "";
        Assert.Contains("no-cache", guestCache);
        var guestHtml = await guestDetailResp.Content.ReadAsStringAsync();
        Assert.Contains("jt-locked-box", guestHtml);
        Assert.DoesNotContain("Moulard duck breasts", guestHtml);
        Assert.DoesNotContain("tight diamond pattern", guestHtml);

        // 4. Approved member (or Admin) views detail: unlocked full content rendered
        var adminDetailResp = await adminClient.GetAsync($"/recipes/{itemSlug}");
        Assert.Equal(HttpStatusCode.OK, adminDetailResp.StatusCode);
        var adminHtml = await adminDetailResp.Content.ReadAsStringAsync();
        Assert.Contains("Moulard duck breasts", adminHtml);
        Assert.Contains("tight diamond pattern", adminHtml);
        Assert.DoesNotContain("jt-locked-box", adminHtml);
    }
}
