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
public class ContestEntryTests(FoundationFixture fixture)
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

        var values = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await Token(client, "/account/login"),
            ["Email"] = adminEmail,
            ["Password"] = adminPassword,
            ["ReturnUrl"] = "/admin"
        };
        var loginResponse = await client.PostAsync("/account/login", new FormUrlEncodedContent(values));
        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
        return client;
    }

    private async Task<Contest> CreateTestContestAsync(ContestType type, ContestStatus status, DateTime opensAt, DateTime closesAt)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var slug = $"test-contest-{type.ToString().ToLower()}-{Guid.NewGuid().ToString("N")[..8]}";
        var contest = new Contest
        {
            Title = $"Test Competition {slug}",
            Slug = slug,
            Summary = "Test competition summary for verification.",
            DescriptionAndRules = "Test guidelines and rules requiring at least twenty characters.",
            Type = type,
            Status = status,
            OpensAtUtc = opensAt,
            ClosesAtUtc = closesAt,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Contests.Add(contest);
        await db.SaveChangesAsync();
        return contest;
    }

    [Fact]
    public async Task Guest_cannot_access_entry_form_or_submit_entry()
    {
        var openContest = await CreateTestContestAsync(ContestType.Recipe, ContestStatus.Published, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(7));
        var guestClient = fixture.NewClient();

        // Guest GET entry form -> challenged to login
        var getRes = await guestClient.GetAsync($"/contests/{openContest.Slug}/entry");
        Assert.Equal(HttpStatusCode.Redirect, getRes.StatusCode);
        Assert.Contains("/account/login", getRes.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);

        // Guest POST entry -> challenged to login and saves nothing
        var form = new Dictionary<string, string>
        {
            ["ContestId"] = openContest.Id.ToString(),
            ["ContestSlug"] = openContest.Slug,
            ["ContestTitle"] = openContest.Title,
            ["ContestType"] = ((int)ContestType.Recipe).ToString(),
            ["Title"] = "Guest Illegal Entry",
            ["Summary"] = "This entry should never be saved to database.",
            ["IngredientsText"] = "1 tbsp Salt",
            ["StepsText"] = "Mix ingredients thoroughly."
        };
        var postRes = await guestClient.PostAsync($"/contests/{openContest.Slug}/entry", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, postRes.StatusCode);
        Assert.Contains("/account/login", postRes.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var entryExists = await db.ContestEntries.AnyAsync(e => e.ContestId == openContest.Id);
        Assert.False(entryExists, "Guest request must not persist any contest entry.");
    }

    [Fact]
    public async Task Member_submits_valid_recipe_entry_to_open_contest_succeeds()
    {
        var openContest = await CreateTestContestAsync(ContestType.Recipe, ContestStatus.Published, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(7));
        var memberClient = fixture.NewClient();
        var memberEmail = NewEmail();
        var memberPassword = NewPassword();
        await RegisterAndLogin(memberClient, memberEmail, memberPassword, "Chef Alice");

        var token = await Token(memberClient, $"/contests/{openContest.Slug}/entry");
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["ContestId"] = openContest.Id.ToString(),
            ["ContestSlug"] = openContest.Slug,
            ["ContestTitle"] = openContest.Title,
            ["ContestType"] = ((int)ContestType.Recipe).ToString(),
            ["Title"] = "Slow Braised Ox Cheek",
            ["Summary"] = "Tender beef braised with port wine and autumnal root vegetables.",
            ["Servings"] = "4",
            ["PrepMinutes"] = "25",
            ["CookMinutes"] = "180",
            ["IngredientsText"] = "1kg Ox Cheeks\n2 Carrots, diced\n1 Bottle Ruby Port",
            ["StepsText"] = "Sear the cheeks on high heat until deeply browned.\nBraise gently in port and aromatics for 3 hours.",
            ["Notes"] = "Best served over buttery potato mousseline.",
            ["ImageUrl"] = "https://example.com/oxcheek.jpg"
        };

        var postRes = await memberClient.PostAsync($"/contests/{openContest.Slug}/entry", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, postRes.StatusCode);
        Assert.Equal("/contests/my-entries", postRes.Headers.Location?.OriginalString);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var entry = await db.ContestEntries
            .Include(e => e.Ingredients)
            .Include(e => e.Steps)
            .Include(e => e.AuthorUser)
            .FirstOrDefaultAsync(e => e.ContestId == openContest.Id && e.Title == "Slow Braised Ox Cheek");

        Assert.NotNull(entry);
        Assert.Equal("Chef Alice", entry.AuthorUser.DisplayName);
        Assert.Equal(ContestType.Recipe, entry.EntryKind);
        Assert.Equal(ContestEntryStatus.Submitted, entry.Status);
        Assert.Equal(4, entry.Servings);
        Assert.Equal(25, entry.PrepMinutes);
        Assert.Equal(180, entry.CookMinutes);
        Assert.Equal(3, entry.Ingredients.Count);
        Assert.Equal(2, entry.Steps.Count);
        Assert.Equal("1kg Ox Cheeks", entry.Ingredients.First(i => i.Position == 1).Name);
        Assert.Equal("Best served over buttery potato mousseline.", entry.ContributorNotes);
    }

    [Fact]
    public async Task Member_submits_valid_tip_entry_to_open_tip_contest_succeeds()
    {
        var openTipContest = await CreateTestContestAsync(ContestType.Tip, ContestStatus.Published, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(7));
        var memberClient = fixture.NewClient();
        var memberEmail = NewEmail();
        var memberPassword = NewPassword();
        await RegisterAndLogin(memberClient, memberEmail, memberPassword, "Chef Bob");

        var token = await Token(memberClient, $"/contests/{openTipContest.Slug}/entry");
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["ContestId"] = openTipContest.Id.ToString(),
            ["ContestSlug"] = openTipContest.Slug,
            ["ContestTitle"] = openTipContest.Title,
            ["ContestType"] = ((int)ContestType.Tip).ToString(),
            ["Title"] = "Vegetable Scrap Glaze Reduction",
            ["Summary"] = "Transforming onion skins and carrot tops into umami-rich finishing syrup.",
            ["TipBody"] = "Simmer roasted vegetable scraps with kombu for 40 minutes, strain, and reduce by half with a touch of tamari. Imparts tremendous savory gloss to roasted mushrooms without extra sodium.",
            ["Notes"] = "Developed in a zero-waste kitchen test."
        };

        var postRes = await memberClient.PostAsync($"/contests/{openTipContest.Slug}/entry", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, postRes.StatusCode);
        Assert.Equal("/contests/my-entries", postRes.Headers.Location?.OriginalString);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var entry = await db.ContestEntries
            .FirstOrDefaultAsync(e => e.ContestId == openTipContest.Id && e.Title == "Vegetable Scrap Glaze Reduction");

        Assert.NotNull(entry);
        Assert.Equal(ContestType.Tip, entry.EntryKind);
        Assert.Contains("Simmer roasted vegetable scraps", entry.TipBody);
    }

    [Fact]
    public async Task Submit_entry_fails_when_contest_is_upcoming()
    {
        var upcomingContest = await CreateTestContestAsync(ContestType.Recipe, ContestStatus.Published, DateTime.UtcNow.AddDays(5), DateTime.UtcNow.AddDays(20));
        var memberClient = fixture.NewClient();
        var memberEmail = NewEmail();
        var memberPassword = NewPassword();
        await RegisterAndLogin(memberClient, memberEmail, memberPassword);

        // GET entry on upcoming redirects to detail
        var getRes = await memberClient.GetAsync($"/contests/{upcomingContest.Slug}/entry");
        Assert.Equal(HttpStatusCode.Redirect, getRes.StatusCode);
        Assert.Contains($"/contests/{upcomingContest.Slug}", getRes.Headers.Location?.OriginalString);

        // Direct POST to upcoming contest fails
        var token = await Token(memberClient, $"/contests/{upcomingContest.Slug}");
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["ContestId"] = upcomingContest.Id.ToString(),
            ["ContestSlug"] = upcomingContest.Slug,
            ["ContestTitle"] = upcomingContest.Title,
            ["ContestType"] = ((int)ContestType.Recipe).ToString(),
            ["Title"] = "Premature Submission",
            ["Summary"] = "This entry should be rejected because contest is upcoming.",
            ["IngredientsText"] = "1 tbsp Salt",
            ["StepsText"] = "Mix well."
        };

        var postRes = await memberClient.PostAsync($"/contests/{upcomingContest.Slug}/entry", new FormUrlEncodedContent(form));
        var html = await postRes.Content.ReadAsStringAsync();
        Assert.Contains("open", html, StringComparison.OrdinalIgnoreCase);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var exists = await db.ContestEntries.AnyAsync(e => e.ContestId == upcomingContest.Id);
        Assert.False(exists, "Upcoming contest must not accept entries.");
    }

    [Fact]
    public async Task Submit_entry_fails_when_contest_is_closed_or_deadline_passed()
    {
        var closedContest = await CreateTestContestAsync(ContestType.Recipe, ContestStatus.Closed, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow.AddDays(-10));
        var memberClient = fixture.NewClient();
        var memberEmail = NewEmail();
        var memberPassword = NewPassword();
        await RegisterAndLogin(memberClient, memberEmail, memberPassword);

        var token = await Token(memberClient, $"/contests/{closedContest.Slug}");
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["ContestId"] = closedContest.Id.ToString(),
            ["ContestSlug"] = closedContest.Slug,
            ["ContestTitle"] = closedContest.Title,
            ["ContestType"] = ((int)ContestType.Recipe).ToString(),
            ["Title"] = "Late Submission",
            ["Summary"] = "This entry was submitted after closing date.",
            ["IngredientsText"] = "1 tbsp Salt",
            ["StepsText"] = "Mix well."
        };

        var postRes = await memberClient.PostAsync($"/contests/{closedContest.Slug}/entry", new FormUrlEncodedContent(form));
        var html = await postRes.Content.ReadAsStringAsync();
        Assert.Contains("closed", html, StringComparison.OrdinalIgnoreCase);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var exists = await db.ContestEntries.AnyAsync(e => e.ContestId == closedContest.Id);
        Assert.False(exists, "Closed contest must not accept entries.");
    }

    [Fact]
    public async Task Submit_entry_fails_when_contest_is_draft_or_archived()
    {
        var draftContest = await CreateTestContestAsync(ContestType.Recipe, ContestStatus.Draft, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(10));
        var memberClient = fixture.NewClient();
        var memberEmail = NewEmail();
        var memberPassword = NewPassword();
        await RegisterAndLogin(memberClient, memberEmail, memberPassword);

        // GET entry on draft returns 404
        var getRes = await memberClient.GetAsync($"/contests/{draftContest.Slug}/entry");
        Assert.Equal(HttpStatusCode.NotFound, getRes.StatusCode);

        // POST entry on draft returns 404
        var token = await Token(memberClient, "/contests");
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["ContestId"] = draftContest.Id.ToString(),
            ["ContestSlug"] = draftContest.Slug,
            ["ContestTitle"] = draftContest.Title,
            ["ContestType"] = ((int)ContestType.Recipe).ToString(),
            ["Title"] = "Draft Target Submission",
            ["Summary"] = "Draft contest must not accept submissions.",
            ["IngredientsText"] = "1 tbsp Salt",
            ["StepsText"] = "Mix well."
        };
        var postRes = await memberClient.PostAsync($"/contests/{draftContest.Slug}/entry", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.NotFound, postRes.StatusCode);
    }

    [Fact]
    public async Task Submit_entry_fails_on_type_mismatch()
    {
        var recipeContest = await CreateTestContestAsync(ContestType.Recipe, ContestStatus.Published, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(7));
        var memberClient = fixture.NewClient();
        var memberEmail = NewEmail();
        var memberPassword = NewPassword();
        await RegisterAndLogin(memberClient, memberEmail, memberPassword);

        var token = await Token(memberClient, $"/contests/{recipeContest.Slug}/entry");
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["ContestId"] = recipeContest.Id.ToString(),
            ["ContestSlug"] = recipeContest.Slug,
            ["ContestTitle"] = recipeContest.Title,
            ["ContestType"] = ((int)ContestType.Tip).ToString(), // Tip to Recipe contest!
            ["Title"] = "Mismatched Tip Submission",
            ["Summary"] = "Attempting to submit tip into recipe competition.",
            ["TipBody"] = "Some knife sharpening technique that doesn't fit a recipe contest."
        };

        var postRes = await memberClient.PostAsync($"/contests/{recipeContest.Slug}/entry", new FormUrlEncodedContent(form));
        var html = await postRes.Content.ReadAsStringAsync();
        Assert.Contains("mismatch", html, StringComparison.OrdinalIgnoreCase);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var exists = await db.ContestEntries.AnyAsync(e => e.ContestId == recipeContest.Id);
        Assert.False(exists, "Type mismatch must not save entry.");
    }

    [Fact]
    public async Task Member_can_edit_own_entry_before_deadline_updates_existing_record()
    {
        var openContest = await CreateTestContestAsync(ContestType.Recipe, ContestStatus.Published, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(7));
        var memberClient = fixture.NewClient();
        var memberEmail = NewEmail();
        var memberPassword = NewPassword();
        await RegisterAndLogin(memberClient, memberEmail, memberPassword, "Chef Charlie");

        // 1. Initial submission
        var token1 = await Token(memberClient, $"/contests/{openContest.Slug}/entry");
        var form1 = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token1,
            ["ContestId"] = openContest.Id.ToString(),
            ["ContestSlug"] = openContest.Slug,
            ["ContestTitle"] = openContest.Title,
            ["ContestType"] = ((int)ContestType.Recipe).ToString(),
            ["Title"] = "Original Salmon Formulation",
            ["Summary"] = "Crispy skin salmon with dill emulsion.",
            ["IngredientsText"] = "2 Salmon Fillets\n1 Lemon",
            ["StepsText"] = "Pan sear salmon skin down for 4 minutes.\nFinish with lemon juice."
        };
        var res1 = await memberClient.PostAsync($"/contests/{openContest.Slug}/entry", new FormUrlEncodedContent(form1));
        Assert.Equal(HttpStatusCode.Redirect, res1.StatusCode);

        // 2. Open entry form again -> pre-populated with existing data
        var entryFormHtml = await memberClient.GetStringAsync($"/contests/{openContest.Slug}/entry");
        Assert.Contains("Original Salmon Formulation", entryFormHtml);
        Assert.Contains("Update Contest Entry", entryFormHtml);

        // 3. Update entry before deadline
        var token2 = await Token(memberClient, $"/contests/{openContest.Slug}/entry");
        var form2 = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token2,
            ["ContestId"] = openContest.Id.ToString(),
            ["ContestSlug"] = openContest.Slug,
            ["ContestTitle"] = openContest.Title,
            ["ContestType"] = ((int)ContestType.Recipe).ToString(),
            ["Title"] = "Refined King Salmon & Brown Butter",
            ["Summary"] = "Updated gourmet formulation with capers and fresh dill.",
            ["IngredientsText"] = "2 King Salmon Fillets\n30g Unsalted Butter\n1 tbsp Capers",
            ["StepsText"] = "Sear salmon skin side down.\nBaste with foaming brown butter and capers.",
            ["Notes"] = "Updated with improved temperature control."
        };
        var res2 = await memberClient.PostAsync($"/contests/{openContest.Slug}/entry", new FormUrlEncodedContent(form2));
        Assert.Equal(HttpStatusCode.Redirect, res2.StatusCode);

        // 4. Verify in DB: still exactly ONE entry for this member and contest, fields updated
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var entries = await db.ContestEntries
            .Include(e => e.Ingredients)
            .Include(e => e.Steps)
            .Where(e => e.ContestId == openContest.Id)
            .ToListAsync();

        Assert.Single(entries);
        var updated = entries[0];
        Assert.Equal("Refined King Salmon & Brown Butter", updated.Title);
        Assert.NotNull(updated.UpdatedAtUtc);
        Assert.Equal(3, updated.Ingredients.Count);
        Assert.Equal("Updated with improved temperature control.", updated.ContributorNotes);
    }

    [Fact]
    public async Task Member_cannot_edit_entry_after_deadline_passed()
    {
        var openContest = await CreateTestContestAsync(ContestType.Recipe, ContestStatus.Published, DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(5));
        var memberClient = fixture.NewClient();
        var memberEmail = NewEmail();
        var memberPassword = NewPassword();
        await RegisterAndLogin(memberClient, memberEmail, memberPassword);

        // Submit initial entry
        var token1 = await Token(memberClient, $"/contests/{openContest.Slug}/entry");
        var form1 = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token1,
            ["ContestId"] = openContest.Id.ToString(),
            ["ContestSlug"] = openContest.Slug,
            ["ContestTitle"] = openContest.Title,
            ["ContestType"] = ((int)ContestType.Recipe).ToString(),
            ["Title"] = "Timely Submitted Dish",
            ["Summary"] = "Submitted well before the deadline.",
            ["IngredientsText"] = "1 Duck Breast",
            ["StepsText"] = "Score skin and render slowly."
        };
        await memberClient.PostAsync($"/contests/{openContest.Slug}/entry", new FormUrlEncodedContent(form1));

        // Now move contest closing date to the past
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var contest = await db.Contests.FindAsync(openContest.Id);
            contest!.ClosesAtUtc = DateTime.UtcNow.AddHours(-1);
            await db.SaveChangesAsync();
        }

        // Attempt to edit post-closing
        var token2 = await Token(memberClient, $"/contests/{openContest.Slug}");
        var form2 = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token2,
            ["ContestId"] = openContest.Id.ToString(),
            ["ContestSlug"] = openContest.Slug,
            ["ContestTitle"] = openContest.Title,
            ["ContestType"] = ((int)ContestType.Recipe).ToString(),
            ["Title"] = "Attempted Post-Deadline Update",
            ["Summary"] = "This modification must be denied by server.",
            ["IngredientsText"] = "1 Duck Breast",
            ["StepsText"] = "Score skin and render slowly."
        };
        var editRes = await memberClient.PostAsync($"/contests/{openContest.Slug}/entry", new FormUrlEncodedContent(form2));
        var html = await editRes.Content.ReadAsStringAsync();
        Assert.Contains("closed", html, StringComparison.OrdinalIgnoreCase);

        // DB title is unchanged
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var entry = await db.ContestEntries.FirstAsync(e => e.ContestId == openContest.Id);
            Assert.Equal("Timely Submitted Dish", entry.Title);
        }
    }

    [Fact]
    public async Task Database_unique_constraint_enforces_one_entry_per_member_per_contest()
    {
        var contest = await CreateTestContestAsync(ContestType.Recipe, ContestStatus.Published, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(7));
        var memberEmail = NewEmail();
        var memberPassword = NewPassword();
        var client = fixture.NewClient();
        await RegisterAndLogin(client, memberEmail, memberPassword);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.FirstAsync(u => u.Email == memberEmail);

        var entry1 = new ContestEntry
        {
            ContestId = contest.Id,
            AuthorUserId = user.Id,
            EntryKind = ContestType.Recipe,
            Title = "Unique Constraint Entry 1",
            Summary = "First entry in direct database insert.",
            Status = ContestEntryStatus.Submitted,
            SubmittedAtUtc = DateTime.UtcNow
        };
        db.ContestEntries.Add(entry1);
        await db.SaveChangesAsync();

        var entry2 = new ContestEntry
        {
            ContestId = contest.Id,
            AuthorUserId = user.Id,
            EntryKind = ContestType.Recipe,
            Title = "Unique Constraint Entry 2",
            Summary = "Duplicate entry attempting direct insert.",
            Status = ContestEntryStatus.Submitted,
            SubmittedAtUtc = DateTime.UtcNow
        };
        db.ContestEntries.Add(entry2);

        await Assert.ThrowsAsync<DbUpdateException>(async () => await db.SaveChangesAsync());
    }

    [Fact]
    public async Task Privacy_invariant_contest_entries_are_never_publicly_visible()
    {
        var secretDishName = $"Secret Private Confit {Guid.NewGuid().ToString("N")[..8]}";
        var openContest = await CreateTestContestAsync(ContestType.Recipe, ContestStatus.Published, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(7));

        var memberClientA = fixture.NewClient();
        var memberEmailA = NewEmail();
        await RegisterAndLogin(memberClientA, memberEmailA, NewPassword(), "Member A");

        var token = await Token(memberClientA, $"/contests/{openContest.Slug}/entry");
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["ContestId"] = openContest.Id.ToString(),
            ["ContestSlug"] = openContest.Slug,
            ["ContestTitle"] = openContest.Title,
            ["ContestType"] = ((int)ContestType.Recipe).ToString(),
            ["Title"] = secretDishName,
            ["Summary"] = "A confidential private competition formulation.",
            ["IngredientsText"] = "1 Whole Duck\n500g Duck Fat",
            ["StepsText"] = "Submerge and cook at 85C for 6 hours."
        };
        await memberClientA.PostAsync($"/contests/{openContest.Slug}/entry", new FormUrlEncodedContent(form));

        // 1. Guest checks public contests catalog, detail, recipes, tips
        var guestClient = fixture.NewClient();
        var contestCatalogHtml = await guestClient.GetStringAsync("/contests");
        Assert.DoesNotContain(secretDishName, contestCatalogHtml);

        var contestDetailHtml = await guestClient.GetStringAsync($"/contests/{openContest.Slug}");
        Assert.DoesNotContain(secretDishName, contestDetailHtml);

        var recipesHtml = await guestClient.GetStringAsync("/recipes");
        Assert.DoesNotContain(secretDishName, recipesHtml);

        // 2. Member B visits contest detail -> does not see Member A's entry
        var memberClientB = fixture.NewClient();
        await RegisterAndLogin(memberClientB, NewEmail(), NewPassword(), "Member B");

        var memberBDetailHtml = await memberClientB.GetStringAsync($"/contests/{openContest.Slug}");
        Assert.DoesNotContain(secretDishName, memberBDetailHtml);
        Assert.Contains("Submit Your Official Entry", memberBDetailHtml);

        // Member B visits My Entries -> sees 0 entries
        var memberBMyEntriesHtml = await memberClientB.GetStringAsync("/contests/my-entries");
        Assert.DoesNotContain(secretDishName, memberBMyEntriesHtml);
        Assert.Contains("No competition entries yet", memberBMyEntriesHtml);
    }

    [Fact]
    public async Task Admin_can_view_contest_entries_read_only_and_non_admin_is_forbidden()
    {
        var openContest = await CreateTestContestAsync(ContestType.Recipe, ContestStatus.Published, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(7));
        var memberClient = fixture.NewClient();
        await RegisterAndLogin(memberClient, NewEmail(), NewPassword(), "Chef Diana");

        var token = await Token(memberClient, $"/contests/{openContest.Slug}/entry");
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["ContestId"] = openContest.Id.ToString(),
            ["ContestSlug"] = openContest.Slug,
            ["ContestTitle"] = openContest.Title,
            ["ContestType"] = ((int)ContestType.Recipe).ToString(),
            ["Title"] = "Diana Truffled Gnocchi",
            ["Summary"] = "Handmade potato gnocchi tossed in black truffle cream.",
            ["IngredientsText"] = "500g Russet Potatoes\n150g Tipo 00 Flour\n1 Egg Yolk",
            ["StepsText"] = "Bake potatoes and rice while hot.\nKnead gently with flour and egg."
        };
        var postRes = await memberClient.PostAsync($"/contests/{openContest.Slug}/entry", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, postRes.StatusCode);

        // 1. Admin visits /admin/contests/{id}/entries -> 200 OK and sees Diana's entry
        var adminClient = await CreateAdminClientAsync();
        var adminEntriesRes = await adminClient.GetAsync($"/admin/contests/{openContest.Id}/entries");
        Assert.Equal(HttpStatusCode.OK, adminEntriesRes.StatusCode);
        var adminEntriesHtml = await adminEntriesRes.Content.ReadAsStringAsync();
        Assert.Contains("Diana Truffled Gnocchi", adminEntriesHtml);
        Assert.Contains("Chef Diana", adminEntriesHtml);
        Assert.Contains("500g Russet Potatoes", adminEntriesHtml);
        Assert.Contains("Read-Only Intake Review", adminEntriesHtml);

        // 2. Regular member visits /admin/contests/{id}/entries -> 403 Forbidden / Access Denied
        var memberAdminRes = await memberClient.GetAsync($"/admin/contests/{openContest.Id}/entries");
        Assert.True(memberAdminRes.StatusCode == HttpStatusCode.Forbidden ||
                    (memberAdminRes.StatusCode == HttpStatusCode.Redirect &&
                     memberAdminRes.Headers.Location?.OriginalString.Contains("access-denied") == true),
                     "Member must not be allowed to access admin contest entries.");

        // 3. Guest visits /admin/contests/{id}/entries -> redirected to login
        var guestClient = fixture.NewClient();
        var guestAdminRes = await guestClient.GetAsync($"/admin/contests/{openContest.Id}/entries");
        Assert.Equal(HttpStatusCode.Redirect, guestAdminRes.StatusCode);
        Assert.Contains("/account/login", guestAdminRes.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Member_B_cannot_view_or_edit_Member_A_entry_via_direct_url_or_id()
    {
        var openContest = await CreateTestContestAsync(ContestType.Recipe, ContestStatus.Published, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(7));

        // Member A creates an entry
        var memberClientA = fixture.NewClient();
        var emailA = NewEmail();
        await RegisterAndLogin(memberClientA, emailA, NewPassword(), "Member Alpha");

        var tokenA = await Token(memberClientA, $"/contests/{openContest.Slug}/entry");
        var formA = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = tokenA,
            ["ContestId"] = openContest.Id.ToString(),
            ["ContestSlug"] = openContest.Slug,
            ["ContestTitle"] = openContest.Title,
            ["ContestType"] = ((int)ContestType.Recipe).ToString(),
            ["Title"] = "Alpha Signature Terrine",
            ["Summary"] = "Duck and pistachio terrine by Member Alpha.",
            ["IngredientsText"] = "500g Duck Liver\n50g Pistachios",
            ["StepsText"] = "Layer in terrine mould and bake in water bath."
        };
        var createRes = await memberClientA.PostAsync($"/contests/{openContest.Slug}/entry", new FormUrlEncodedContent(formA));
        Assert.Equal(HttpStatusCode.Redirect, createRes.StatusCode);

        int entryIdA;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var entryA = await db.ContestEntries.FirstAsync(e => e.ContestId == openContest.Id);
            entryIdA = entryA.Id;
        }

        // Member B logs in
        var memberClientB = fixture.NewClient();
        var emailB = NewEmail();
        await RegisterAndLogin(memberClientB, emailB, NewPassword(), "Member Bravo");

        // 1. Member B attempts to view Member A's entry via /contests/{slug}/my-entry -> 404 (Member B has no entry in this contest)
        var memberBSlugRes = await memberClientB.GetAsync($"/contests/{openContest.Slug}/my-entry");
        Assert.Equal(HttpStatusCode.NotFound, memberBSlugRes.StatusCode);

        // 2. Member B attempts to view Member A's entry via /contests/my-entries/{entryIdA} -> 404
        var memberBIdRes = await memberClientB.GetAsync($"/contests/my-entries/{entryIdA}");
        Assert.Equal(HttpStatusCode.NotFound, memberBIdRes.StatusCode);

        // 3. Member B attempts to edit Member A's entry by sending Member A's entry ID in POST
        var tokenB = await Token(memberClientB, $"/contests/{openContest.Slug}/entry");
        var formB = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = tokenB,
            ["EntryId"] = entryIdA.ToString(),
            ["ContestId"] = openContest.Id.ToString(),
            ["ContestSlug"] = openContest.Slug,
            ["ContestTitle"] = openContest.Title,
            ["ContestType"] = ((int)ContestType.Recipe).ToString(),
            ["Title"] = "Malicious Hijack Attempt",
            ["Summary"] = "Bravo attempting to overwrite Alpha entry.",
            ["IngredientsText"] = "1 Onion",
            ["StepsText"] = "Chop onion."
        };
        var editRes = await memberClientB.PostAsync($"/contests/{openContest.Slug}/entry", new FormUrlEncodedContent(formB));
        Assert.Equal(HttpStatusCode.Redirect, editRes.StatusCode);

        // Verify Member A's entry was completely untouched
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var entryA = await db.ContestEntries
                .Include(e => e.Ingredients)
                .Include(e => e.Steps)
                .FirstAsync(e => e.Id == entryIdA);

            Assert.Equal("Alpha Signature Terrine", entryA.Title);
            Assert.Equal("Duck and pistachio terrine by Member Alpha.", entryA.Summary);
            Assert.Equal(2, entryA.Ingredients.Count);
            Assert.Single(entryA.Steps);

            // And Member B's post created Member B's OWN entry instead
            var userB = await db.Users.FirstAsync(u => u.Email == emailB);
            var entryB = await db.ContestEntries.FirstAsync(e => e.AuthorUserId == userB.Id && e.ContestId == openContest.Id);
            Assert.Equal("Malicious Hijack Attempt", entryB.Title);
            Assert.NotEqual(entryIdA, entryB.Id);
        }
    }

    [Fact]
    public async Task Forged_POST_payload_cannot_tamper_with_author_contest_status_or_submitted_date()
    {
        var openContest = await CreateTestContestAsync(ContestType.Recipe, ContestStatus.Published, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(7));
        var client = fixture.NewClient();
        var email = NewEmail();
        await RegisterAndLogin(client, email, NewPassword(), "Honest Member");

        // Attacker attempts to forge AuthorUserId, ContestId, Status, SubmittedAtUtc
        var token = await Token(client, $"/contests/{openContest.Slug}/entry");
        var forgedPayload = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["AuthorUserId"] = "fake-user-id-99999",
            ["ContestId"] = "999999",
            ["ContestSlug"] = openContest.Slug,
            ["ContestTitle"] = openContest.Title,
            ["ContestType"] = ((int)ContestType.Recipe).ToString(),
            ["Status"] = ((int)ContestEntryStatus.Selected).ToString(),
            ["SubmittedAtUtc"] = "2020-01-01T00:00:00Z",
            ["Title"] = "Forged Metadata Dish",
            ["Summary"] = "Attempting to inject fake status and historical timestamp.",
            ["IngredientsText"] = "1 Whole Truffle",
            ["StepsText"] = "Shave truffle generously."
        };
        var res = await client.PostAsync($"/contests/{openContest.Slug}/entry", new FormUrlEncodedContent(forgedPayload));
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.FirstAsync(u => u.Email == email);
        var entry = await db.ContestEntries.FirstAsync(e => e.ContestId == openContest.Id && e.AuthorUserId == user.Id);

        // Assert strictly bound server values
        Assert.Equal(user.Id, entry.AuthorUserId);
        Assert.Equal(openContest.Id, entry.ContestId);
        Assert.Equal(ContestEntryStatus.Submitted, entry.Status); // NOT Selected
        Assert.True(entry.SubmittedAtUtc > DateTime.UtcNow.AddMinutes(-5)); // NOT 2020
    }

    [Fact]
    public async Task Post_entry_requires_antiforgery_token()
    {
        var openContest = await CreateTestContestAsync(ContestType.Recipe, ContestStatus.Published, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(7));
        var client = fixture.NewClient();
        await RegisterAndLogin(client, NewEmail(), NewPassword());

        // POST without token
        var form = new Dictionary<string, string>
        {
            ["ContestId"] = openContest.Id.ToString(),
            ["ContestSlug"] = openContest.Slug,
            ["ContestType"] = ((int)ContestType.Recipe).ToString(),
            ["Title"] = "Missing Token Dish",
            ["Summary"] = "This request must fail antiforgery verification.",
            ["IngredientsText"] = "1 Salt",
            ["StepsText"] = "Season to taste."
        };
        var res = await client.PostAsync($"/contests/{openContest.Slug}/entry", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Date_boundary_and_closed_contest_enforcement()
    {
        var contest = await CreateTestContestAsync(ContestType.Tip, ContestStatus.Published, DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddHours(2));
        var client = fixture.NewClient();
        var email = NewEmail();
        await RegisterAndLogin(client, email, NewPassword(), "Boundary Tester");

        // 1. Admin closes contest manually before ClosesAtUtc
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var c = await db.Contests.FindAsync(contest.Id);
            c!.Status = ContestStatus.Closed;
            await db.SaveChangesAsync();
        }

        // Submitting to closed contest is rejected
        var token = await Token(client, $"/contests/{contest.Slug}");
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["ContestId"] = contest.Id.ToString(),
            ["ContestSlug"] = contest.Slug,
            ["ContestTitle"] = contest.Title,
            ["ContestType"] = ((int)ContestType.Tip).ToString(),
            ["Title"] = "Late Tip Submission",
            ["Summary"] = "Testing submission to admin-closed contest.",
            ["TipBody"] = "Always chill puff pastry before baking at high temperature."
        };
        var res = await client.PostAsync($"/contests/{contest.Slug}/entry", new FormUrlEncodedContent(form));
        var html = await res.Content.ReadAsStringAsync();
        Assert.Contains("concluded", html, StringComparison.OrdinalIgnoreCase);

        // Verify no entry was created in DB
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var exists = await db.ContestEntries.AnyAsync(e => e.ContestId == contest.Id);
            Assert.False(exists, "No entry should be created for closed contest.");
        }
    }
}
