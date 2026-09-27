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
public class ContestTests(FoundationFixture fixture)
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
            ["ReturnUrl"] = "/"
        };
        var response = await client.PostAsync("/account/login", new FormUrlEncodedContent(values));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    [Fact]
    public async Task Admin_CanCreatePublishedRecipeContest_AppearsOnPublicListingAndDetail()
    {
        var adminClient = await CreateAdminClientAsync();
        var title = "Seasonal Roast Challenge " + Guid.NewGuid().ToString("N")[..8];
        var opensAt = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-ddTHH:mm");
        var closesAt = DateTime.UtcNow.AddDays(14).ToString("yyyy-MM-ddTHH:mm");

        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await Token(adminClient, "/admin/contests/new"),
            ["Title"] = title,
            ["Summary"] = "A showcase of traditional and modern roast formulas.",
            ["DescriptionAndRules"] = "Submit complete ingredients and oven temperature guides. Open to registered members.",
            ["Type"] = ((int)ContestType.Recipe).ToString(),
            ["Status"] = ((int)ContestStatus.Published).ToString(),
            ["PrizeDescription"] = "Chef James Thew Gold Medal",
            ["OpensAt"] = opensAt,
            ["ClosesAt"] = closesAt
        };

        var postResponse = await adminClient.PostAsync("/admin/contests/new", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);
        Assert.Equal("/admin/contests", postResponse.Headers.Location?.OriginalString);

        // Verify in database
        await using var db = fixture.CreateDb();
        var contest = await db.Contests.FirstOrDefaultAsync(c => c.Title == title);
        Assert.NotNull(contest);
        Assert.Equal(ContestType.Recipe, contest.Type);
        Assert.Equal(ContestStatus.Published, contest.Status);
        Assert.NotEmpty(contest.Slug);

        // Verify guest public listing
        var guestClient = fixture.NewClient();
        var listingHtml = await guestClient.GetStringAsync("/contests");
        Assert.Contains(title, listingHtml);
        Assert.Contains(contest.Slug, listingHtml);

        // Verify guest public detail
        var detailHtml = await guestClient.GetStringAsync($"/contests/{contest.Slug}");
        Assert.Contains(title, detailHtml);
        Assert.Contains("A showcase of traditional and modern roast formulas.", detailHtml);
        Assert.Contains("Chef James Thew Gold Medal", detailHtml);
    }

    [Fact]
    public async Task Admin_CanCreateTipContest_WithCustomSlug()
    {
        var adminClient = await CreateAdminClientAsync();
        var customSlug = "pastry-folding-technique-" + Guid.NewGuid().ToString("N")[..6];
        var title = "Pastry Folding Mastery " + Guid.NewGuid().ToString("N")[..6];

        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await Token(adminClient, "/admin/contests/new"),
            ["Title"] = title,
            ["Slug"] = customSlug,
            ["Summary"] = "Share secret techniques for laminated dough and butter blocks.",
            ["DescriptionAndRules"] = "Clear, actionable tips under 400 words explaining temperature control.",
            ["Type"] = ((int)ContestType.Tip).ToString(),
            ["Status"] = ((int)ContestStatus.Published).ToString(),
            ["PrizeDescription"] = "Baking Master Distinction",
            ["OpensAt"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm"),
            ["ClosesAt"] = DateTime.UtcNow.AddDays(7).ToString("yyyy-MM-ddTHH:mm")
        };

        var postResponse = await adminClient.PostAsync("/admin/contests/new", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);

        await using var db = fixture.CreateDb();
        var contest = await db.Contests.FirstOrDefaultAsync(c => c.Slug == customSlug);
        Assert.NotNull(contest);
        Assert.Equal(ContestType.Tip, contest.Type);
    }

    [Fact]
    public async Task Admin_CreateContest_WithDuplicateSlug_AutoResolvesWithNumericSuffix()
    {
        var adminClient = await CreateAdminClientAsync();
        var baseTitle = "Unique Sourdough Challenge " + Guid.NewGuid().ToString("N")[..6];

        var form1 = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await Token(adminClient, "/admin/contests/new"),
            ["Title"] = baseTitle,
            ["Summary"] = "First edition of the sourdough competition.",
            ["DescriptionAndRules"] = "Rules for sourdough competition.",
            ["Type"] = ((int)ContestType.Recipe).ToString(),
            ["Status"] = ((int)ContestStatus.Published).ToString(),
            ["OpensAt"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm"),
            ["ClosesAt"] = DateTime.UtcNow.AddDays(10).ToString("yyyy-MM-ddTHH:mm")
        };
        var res1 = await adminClient.PostAsync("/admin/contests/new", new FormUrlEncodedContent(form1));
        Assert.Equal(HttpStatusCode.Redirect, res1.StatusCode);

        // Submit second contest with same title and no custom slug (will attempt same slug)
        var form2 = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await Token(adminClient, "/admin/contests/new"),
            ["Title"] = baseTitle,
            ["Summary"] = "Second edition with same title.",
            ["DescriptionAndRules"] = "Rules for second sourdough competition.",
            ["Type"] = ((int)ContestType.Recipe).ToString(),
            ["Status"] = ((int)ContestStatus.Published).ToString(),
            ["OpensAt"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm"),
            ["ClosesAt"] = DateTime.UtcNow.AddDays(10).ToString("yyyy-MM-ddTHH:mm")
        };
        var res2 = await adminClient.PostAsync("/admin/contests/new", new FormUrlEncodedContent(form2));
        Assert.Equal(HttpStatusCode.Redirect, res2.StatusCode);

        await using var db = fixture.CreateDb();
        var contests = await db.Contests.Where(c => c.Title == baseTitle).OrderBy(c => c.Id).ToListAsync();
        Assert.Equal(2, contests.Count);
        Assert.NotEqual(contests[0].Slug, contests[1].Slug);
        Assert.StartsWith(contests[0].Slug, contests[1].Slug);
        Assert.EndsWith("-2", contests[1].Slug);
    }

    [Fact]
    public async Task Admin_CreateContest_WithInvalidDateOrder_IsRejectedWithModelError()
    {
        var adminClient = await CreateAdminClientAsync();
        var title = "Invalid Date Contest " + Guid.NewGuid().ToString("N")[..6];

        // Closing time is BEFORE opening time
        var opensAt = DateTime.UtcNow.AddDays(10).ToString("yyyy-MM-ddTHH:mm");
        var closesAt = DateTime.UtcNow.AddDays(2).ToString("yyyy-MM-ddTHH:mm");

        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await Token(adminClient, "/admin/contests/new"),
            ["Title"] = title,
            ["Summary"] = "This contest has invalid dates.",
            ["DescriptionAndRules"] = "Rules description must be at least twenty characters long.",
            ["Type"] = ((int)ContestType.Recipe).ToString(),
            ["Status"] = ((int)ContestStatus.Draft).ToString(),
            ["OpensAt"] = opensAt,
            ["ClosesAt"] = closesAt
        };

        var postResponse = await adminClient.PostAsync("/admin/contests/new", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode); // Returns View with validation errors

        var html = await postResponse.Content.ReadAsStringAsync();
        Assert.Contains("Closing date and time must be strictly later than the opening date", html);

        await using var db = fixture.CreateDb();
        var contest = await db.Contests.FirstOrDefaultAsync(c => c.Title == title);
        Assert.Null(contest);
    }

    [Fact]
    public async Task Admin_CanPublishAndUnpublishContest_DraftIsHiddenFromPublic()
    {
        var adminClient = await CreateAdminClientAsync();
        var title = "Draft Secret Contest " + Guid.NewGuid().ToString("N")[..6];

        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await Token(adminClient, "/admin/contests/new"),
            ["Title"] = title,
            ["Summary"] = "Editorial secret draft not meant for public viewing.",
            ["DescriptionAndRules"] = "Rules description must be at least twenty characters long.",
            ["Type"] = ((int)ContestType.Tip).ToString(),
            ["Status"] = ((int)ContestStatus.Draft).ToString(),
            ["OpensAt"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm"),
            ["ClosesAt"] = DateTime.UtcNow.AddDays(10).ToString("yyyy-MM-ddTHH:mm")
        };

        var createRes = await adminClient.PostAsync("/admin/contests/new", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, createRes.StatusCode);

        await using var db = fixture.CreateDb();
        var contest = await db.Contests.FirstOrDefaultAsync(c => c.Title == title);
        Assert.NotNull(contest);
        Assert.Equal(ContestStatus.Draft, contest.Status);

        // Guest cannot see on listing
        var guestClient = fixture.NewClient();
        var listingHtml = await guestClient.GetStringAsync("/contests");
        Assert.DoesNotContain(title, listingHtml);

        // Guest cannot see via direct slug URL -> 404
        var detailRes = await guestClient.GetAsync($"/contests/{contest.Slug}");
        Assert.Equal(HttpStatusCode.NotFound, detailRes.StatusCode);

        // Admin publishes it
        var pubForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await Token(adminClient, "/admin/contests")
        };
        var pubRes = await adminClient.PostAsync($"/admin/contests/{contest.Id}/publish", new FormUrlEncodedContent(pubForm));
        Assert.Equal(HttpStatusCode.Redirect, pubRes.StatusCode);

        // Guest now sees on listing and detail
        var publishedDetailRes = await guestClient.GetAsync($"/contests/{contest.Slug}");
        Assert.Equal(HttpStatusCode.OK, publishedDetailRes.StatusCode);

        // Admin unpublishes it back to Draft
        var unpubForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await Token(adminClient, "/admin/contests")
        };
        var unpubRes = await adminClient.PostAsync($"/admin/contests/{contest.Id}/unpublish", new FormUrlEncodedContent(unpubForm));
        Assert.Equal(HttpStatusCode.Redirect, unpubRes.StatusCode);

        // Guest direct URL returns 404 again
        var hiddenDetailRes = await guestClient.GetAsync($"/contests/{contest.Slug}");
        Assert.Equal(HttpStatusCode.NotFound, hiddenDetailRes.StatusCode);
    }

    [Fact]
    public async Task Admin_CanCloseArchiveAndRestoreContest()
    {
        var adminClient = await CreateAdminClientAsync();
        var title = "Contest Lifecycle " + Guid.NewGuid().ToString("N")[..6];

        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await Token(adminClient, "/admin/contests/new"),
            ["Title"] = title,
            ["Summary"] = "Contest to test close, archive, and restore transitions.",
            ["DescriptionAndRules"] = "Rules description must be at least twenty characters long.",
            ["Type"] = ((int)ContestType.Recipe).ToString(),
            ["Status"] = ((int)ContestStatus.Published).ToString(),
            ["OpensAt"] = DateTime.UtcNow.AddDays(-5).ToString("yyyy-MM-ddTHH:mm"),
            ["ClosesAt"] = DateTime.UtcNow.AddDays(5).ToString("yyyy-MM-ddTHH:mm")
        };
        await adminClient.PostAsync("/admin/contests/new", new FormUrlEncodedContent(form));

        await using var db = fixture.CreateDb();
        var contest = await db.Contests.FirstOrDefaultAsync(c => c.Title == title);
        Assert.NotNull(contest);

        // Close contest
        var token = await Token(adminClient, "/admin/contests");
        var closeRes = await adminClient.PostAsync($"/admin/contests/{contest.Id}/close", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Redirect, closeRes.StatusCode);

        await using var db2 = fixture.CreateDb();
        var closedContest = await db2.Contests.FindAsync(contest.Id);
        Assert.Equal(ContestStatus.Closed, closedContest?.Status);

        // Closed contest MUST remain publicly browseable under Past Contests and direct URL
        var guestClient = fixture.NewClient();
        var allContestsHtml = await guestClient.GetStringAsync("/contests");
        Assert.Contains(title, allContestsHtml);

        var pastContestsHtml = await guestClient.GetStringAsync("/contests?phase=Ended");
        Assert.Contains(title, pastContestsHtml);

        var openContestsHtml = await guestClient.GetStringAsync("/contests?phase=Open");
        Assert.DoesNotContain(title, openContestsHtml);

        var closedDetailRes = await guestClient.GetAsync($"/contests/{contest.Slug}");
        Assert.Equal(HttpStatusCode.OK, closedDetailRes.StatusCode);
        var closedDetailHtml = await closedDetailRes.Content.ReadAsStringAsync();
        Assert.Contains("Submissions Concluded", closedDetailHtml);
        Assert.Contains("This contest is now closed to new submissions", closedDetailHtml);

        // Archive contest
        var token2 = await Token(adminClient, "/admin/contests");
        var archiveRes = await adminClient.PostAsync($"/admin/contests/{contest.Id}/archive", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token2
        }));
        Assert.Equal(HttpStatusCode.Redirect, archiveRes.StatusCode);

        await using var db3 = fixture.CreateDb();
        var archivedContest = await db3.Contests.FindAsync(contest.Id);
        Assert.Equal(ContestStatus.Archived, archivedContest?.Status);
        Assert.NotNull(archivedContest?.DeletedAtUtc);

        // Archived contest is inaccessible publicly from catalog and direct URL returns 404
        var afterArchiveHtml = await guestClient.GetStringAsync("/contests");
        Assert.DoesNotContain(title, afterArchiveHtml);

        var guestRes = await guestClient.GetAsync($"/contests/{contest.Slug}");
        Assert.Equal(HttpStatusCode.NotFound, guestRes.StatusCode);

        // Restore contest
        var token3 = await Token(adminClient, "/admin/contests");
        var restoreRes = await adminClient.PostAsync($"/admin/contests/{contest.Id}/restore", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token3
        }));
        Assert.Equal(HttpStatusCode.Redirect, restoreRes.StatusCode);

        await using var db4 = fixture.CreateDb();
        var restoredContest = await db4.Contests.FindAsync(contest.Id);
        Assert.Null(restoredContest?.DeletedAtUtc);
        Assert.Equal(ContestStatus.Draft, restoredContest?.Status);
    }

    [Fact]
    public void DateBoundary_OpeningAndClosingInstants_AreCalculatedCorrectly()
    {
        var fixedOpens = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        var fixedCloses = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);

        var contest = new Contest
        {
            Status = ContestStatus.Published,
            OpensAtUtc = fixedOpens,
            ClosesAtUtc = fixedCloses
        };

        // 1. One tick before opening -> Upcoming
        Assert.Equal(ContestTimelinePhase.Upcoming, contest.GetTimelinePhase(fixedOpens.AddTicks(-1)));

        // 2. Exactly at opening instant -> Open
        Assert.Equal(ContestTimelinePhase.Open, contest.GetTimelinePhase(fixedOpens));

        // 3. During open window -> Open
        Assert.Equal(ContestTimelinePhase.Open, contest.GetTimelinePhase(fixedOpens.AddDays(3)));

        // 4. Exactly at closing instant -> Open
        Assert.Equal(ContestTimelinePhase.Open, contest.GetTimelinePhase(fixedCloses));

        // 5. One tick after closing instant -> Ended
        Assert.Equal(ContestTimelinePhase.Ended, contest.GetTimelinePhase(fixedCloses.AddTicks(1)));

        // 6. Manually Closed by admin during active dates -> Ended, IsActiveOpen = false
        contest.Status = ContestStatus.Closed;
        Assert.Equal(ContestTimelinePhase.Ended, contest.GetTimelinePhase(fixedOpens.AddDays(3)));
        Assert.False(contest.IsActiveOpen);
    }

    [Fact]
    public async Task NonAdmin_CannotAccessAdminContestEndpoints()
    {
        var guestClient = fixture.NewClient();

        // Guest GET /admin/contests -> redirects to login
        var getRes = await guestClient.GetAsync("/admin/contests");
        Assert.Equal(HttpStatusCode.Redirect, getRes.StatusCode);
        Assert.Contains("/account/login", getRes.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);

        // Guest POST /admin/contests/new -> redirects to login
        var postRes = await guestClient.PostAsync("/admin/contests/new", new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.Redirect, postRes.StatusCode);
        Assert.Contains("/account/login", postRes.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);

        // Member GET /admin/contests -> 403 Forbidden or AccessDenied
        var memberClient = fixture.NewClient();
        var email = NewEmail();
        var password = NewPassword();
        await RegisterAndLogin(memberClient, email, password);

        var memberGetRes = await memberClient.GetAsync("/admin/contests");
        Assert.True(
            memberGetRes.StatusCode == HttpStatusCode.Forbidden ||
            (memberGetRes.StatusCode == HttpStatusCode.Redirect && memberGetRes.Headers.Location?.OriginalString.Contains("/account/access-denied", StringComparison.OrdinalIgnoreCase) == true),
            $"Expected 403 Forbidden or access-denied redirect, got {memberGetRes.StatusCode} with location {memberGetRes.Headers.Location}");

        var memberPostRes = await memberClient.PostAsync("/admin/contests/new", new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.True(
            memberPostRes.StatusCode == HttpStatusCode.Forbidden ||
            (memberPostRes.StatusCode == HttpStatusCode.Redirect && memberPostRes.Headers.Location?.OriginalString.Contains("/account/access-denied", StringComparison.OrdinalIgnoreCase) == true),
            $"Expected 403 Forbidden or access-denied redirect, got {memberPostRes.StatusCode} with location {memberPostRes.Headers.Location}");
    }

    [Fact]
    public async Task NonAdmin_CannotExecuteStatusActions_RedirectsAppropriately()
    {
        var guestClient = fixture.NewClient();
        var memberClient = fixture.NewClient();
        await RegisterAndLogin(memberClient, NewEmail(), NewPassword());

        var endpoints = new[] { "publish", "unpublish", "close", "archive", "restore" };
        foreach (var action in endpoints)
        {
            // Guest -> redirects to login
            var guestRes = await guestClient.PostAsync($"/admin/contests/1/{action}", new FormUrlEncodedContent(new Dictionary<string, string>()));
            Assert.Equal(HttpStatusCode.Redirect, guestRes.StatusCode);
            Assert.Contains("/account/login", guestRes.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);

            // Member -> redirects to access-denied or 403
            var memberRes = await memberClient.PostAsync($"/admin/contests/1/{action}", new FormUrlEncodedContent(new Dictionary<string, string>()));
            Assert.True(
                memberRes.StatusCode == HttpStatusCode.Forbidden ||
                (memberRes.StatusCode == HttpStatusCode.Redirect && memberRes.Headers.Location?.OriginalString.Contains("/account/access-denied", StringComparison.OrdinalIgnoreCase) == true),
                $"Expected 403 or access-denied redirect for member on action {action}, got {memberRes.StatusCode}");
        }
    }

    [Fact]
    public async Task AdminContestEndpoints_RejectPostWithoutAntiforgeryToken()
    {
        var adminClient = await CreateAdminClientAsync();

        var formWithoutToken = new Dictionary<string, string>
        {
            ["Title"] = "Test Contest",
            ["Summary"] = "Test summary",
            ["DescriptionAndRules"] = "Test description with twenty characters or more.",
            ["Type"] = "1",
            ["Status"] = "1",
            ["OpensAt"] = DateTime.UtcNow.ToString("s"),
            ["ClosesAt"] = DateTime.UtcNow.AddDays(7).ToString("s")
        };

        var response = await adminClient.PostAsync("/admin/contests/new", new FormUrlEncodedContent(formWithoutToken));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
