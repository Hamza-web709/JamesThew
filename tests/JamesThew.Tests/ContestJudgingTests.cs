using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using JamesThew.Data;
using JamesThew.Models;
using JamesThew.Services;
using JamesThew.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace JamesThew.Tests;

[Collection("Foundation")]
public class ContestJudgingTests(FoundationFixture fixture)
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

    private async Task<(HttpClient Client, string AdminUserId)> CreateAdminClientAsync()
    {
        var client = fixture.NewClient();
        var adminEmail = NewEmail();
        var adminPassword = NewPassword();
        string adminUserId;

        using (var scope = fixture.Services.CreateScope())
        {
            var env = new DevEnv();
            var config = SeedConfig(adminEmail, adminPassword);
            await IdentitySeeder.SeedLocalAdminAsync(scope.ServiceProvider, config, env);
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == adminEmail);
            adminUserId = user.Id;
        }

        var values = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await Token(client, "/account/login"),
            ["Email"] = adminEmail,
            ["Password"] = adminPassword,
            ["ReturnUrl"] = "/admin"
        };
        var res = await client.PostAsync("/account/login", new FormUrlEncodedContent(values));
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);

        return (client, adminUserId);
    }

    private async Task<(Contest Contest, ContestEntry Entry, ApplicationUser Author)> CreateContestWithEntryAsync(
        ContestStatus contestStatus = ContestStatus.Published,
        DateTime? opensAtUtc = null,
        DateTime? closesAtUtc = null,
        ContestType type = ContestType.Recipe)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var author = new ApplicationUser
        {
            UserName = NewEmail(),
            Email = NewEmail(),
            DisplayName = "Chef " + Guid.NewGuid().ToString("N")[..8],
            EmailConfirmed = true
        };
        db.Users.Add(author);

        var contest = new Contest
        {
            Title = "Competition " + Guid.NewGuid().ToString("N")[..8],
            Slug = "contest-" + Guid.NewGuid().ToString("N")[..8],
            Summary = "Summary for competition testing.",
            DescriptionAndRules = "Detailed competition rules.",
            Type = type,
            Status = contestStatus,
            PrizeDescription = "Golden Whisk Distinction",
            OpensAtUtc = opensAtUtc ?? DateTime.UtcNow.AddDays(-10),
            ClosesAtUtc = closesAtUtc ?? DateTime.UtcNow.AddDays(-1)
        };
        db.Contests.Add(contest);
        await db.SaveChangesAsync();

        var entry = new ContestEntry
        {
            ContestId = contest.Id,
            AuthorUserId = author.Id,
            Title = "Winning Candidate Dish",
            Summary = "Delightful secret formulation.",
            Status = ContestEntryStatus.Submitted,
            SubmittedAtUtc = DateTime.UtcNow.AddDays(-2),
            Ingredients =
            [
                new ContestEntryIngredient { Name = "Butter", QuantityText = "100", Unit = "g", Position = 1 },
                new ContestEntryIngredient { Name = "Secret Seasoning", QuantityText = "1", Unit = "tsp", Position = 2 }
            ],
            Steps =
            [
                new ContestEntryStep { Instruction = "Step 1: Mix butter", Position = 1 },
                new ContestEntryStep { Instruction = "Step 2: Bake at 200C", Position = 2 }
            ]
        };
        db.ContestEntries.Add(entry);
        await db.SaveChangesAsync();

        return (contest, entry, author);
    }

    [Fact]
    public async Task Admin_cannot_select_winner_prematurely_while_submission_window_is_open()
    {
        // Contest is open (ClosesAtUtc in the future, Status == Published)
        var (contest, entry, _) = await CreateContestWithEntryAsync(
            contestStatus: ContestStatus.Published,
            opensAtUtc: DateTime.UtcNow.AddDays(-2),
            closesAtUtc: DateTime.UtcNow.AddDays(5));

        using var scope = fixture.Services.CreateScope();
        var judgingService = scope.ServiceProvider.GetRequiredService<IContestJudgingService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var admin = await db.Users.FirstAsync();

        var (success, message) = await judgingService.SelectWinnerAsync(contest.Id, entry.Id, admin.Id);

        Assert.False(success);
        Assert.Contains("active", message, StringComparison.OrdinalIgnoreCase);

        // Verify contest has no winner in DB
        var reloadedContest = await db.Contests.FindAsync(contest.Id);
        Assert.Null(reloadedContest!.WinningEntryId);
        Assert.Null(reloadedContest.WinnerSelectedAtUtc);
    }

    [Fact]
    public async Task Admin_cannot_select_entry_from_different_contest()
    {
        var (contestA, _, _) = await CreateContestWithEntryAsync(closesAtUtc: DateTime.UtcNow.AddDays(-1));
        var (_, entryB, _) = await CreateContestWithEntryAsync(closesAtUtc: DateTime.UtcNow.AddDays(-1));

        using var scope = fixture.Services.CreateScope();
        var judgingService = scope.ServiceProvider.GetRequiredService<IContestJudgingService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var admin = await db.Users.FirstAsync();

        var (success, message) = await judgingService.SelectWinnerAsync(contestA.Id, entryB.Id, admin.Id);

        Assert.False(success);
        Assert.Contains("does not belong", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Admin_cannot_select_disqualified_entry_as_winner()
    {
        var (contest, entry, _) = await CreateContestWithEntryAsync(closesAtUtc: DateTime.UtcNow.AddDays(-1));

        using var scope = fixture.Services.CreateScope();
        var judgingService = scope.ServiceProvider.GetRequiredService<IContestJudgingService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var admin = await db.Users.FirstAsync();

        // Mark entry as disqualified first
        await judgingService.ReviewEntryAsync(contest.Id, entry.Id, admin.Id, ContestEntryStatus.Disqualified, "Private note: plagiarism", "Violated original recipe policy");

        var (success, message) = await judgingService.SelectWinnerAsync(contest.Id, entry.Id, admin.Id);

        Assert.False(success);
        Assert.Contains("disqualified", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Admin_qualitative_review_stores_private_notes_and_never_leaks_to_member()
    {
        var (contest, entry, author) = await CreateContestWithEntryAsync(closesAtUtc: DateTime.UtcNow.AddDays(-1));

        using var scope = fixture.Services.CreateScope();
        var judgingService = scope.ServiceProvider.GetRequiredService<IContestJudgingService>();
        var entryService = scope.ServiceProvider.GetRequiredService<IContestEntryService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var admin = await db.Users.FirstAsync();

        const string privateNote = "Confidential jury remark: superb texture, slightly high sodium.";
        const string publicReason = "Exceeded seasoning criteria";

        var (success, _) = await judgingService.ReviewEntryAsync(contest.Id, entry.Id, admin.Id, ContestEntryStatus.UnderReview, privateNote, publicReason);
        Assert.True(success);

        // Admin entries list includes private notes
        var adminEntries = await entryService.GetAdminContestEntriesAsync(contest.Id);
        Assert.NotNull(adminEntries);
        var adminRow = adminEntries!.Entries.First(e => e.EntryId == entry.Id);
        Assert.Equal(privateNote, adminRow.AdminReviewNotes);
        Assert.Equal(ContestEntryStatus.UnderReview, adminRow.Status);

        // Member entry detail does NOT leak private review notes
        var memberDetail = await entryService.GetMemberEntryDetailAsync(entry.Id, author.Id);
        Assert.NotNull(memberDetail);
        Assert.Equal(ContestEntryStatus.UnderReview, memberDetail!.Status);

        // Member entries list does NOT leak private notes
        var myEntries = await entryService.GetMyEntriesAsync(author.Id);
        var myCard = myEntries.Entries.First(e => e.EntryId == entry.Id);
        Assert.Equal(ContestEntryStatus.UnderReview, myCard.Status);
    }

    [Fact]
    public async Task Member_cannot_edit_entry_when_under_review_disqualified_or_selected_even_if_contest_is_open()
    {
        // Contest is open
        var (contest, entry, author) = await CreateContestWithEntryAsync(
            contestStatus: ContestStatus.Published,
            opensAtUtc: DateTime.UtcNow.AddDays(-2),
            closesAtUtc: DateTime.UtcNow.AddDays(5));

        using var scope = fixture.Services.CreateScope();
        var judgingService = scope.ServiceProvider.GetRequiredService<IContestJudgingService>();
        var entryService = scope.ServiceProvider.GetRequiredService<IContestEntryService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var admin = await db.Users.FirstAsync();

        var editModel = new ContestEntryFormViewModel
        {
            ContestId = contest.Id,
            ContestSlug = contest.Slug,
            ContestTitle = contest.Title,
            ContestType = contest.Type,
            Title = "Attempted Forged Update",
            Summary = "Attempted summary update",
            IngredientsText = "1 egg",
            StepsText = "Fry egg"
        };

        // Case 1: Status = UnderReview
        await judgingService.ReviewEntryAsync(contest.Id, entry.Id, admin.Id, ContestEntryStatus.UnderReview, "Reviewing now", null);
        var formUnderReview = await entryService.GetEntryFormAsync(contest.Slug, author.Id);
        Assert.True(formUnderReview!.IsReadonly);
        var (success1, message1, _) = await entryService.SubmitOrUpdateEntryAsync(contest.Slug, author.Id, editModel);
        Assert.False(success1);
        Assert.Contains("can no longer be edited", message1, StringComparison.OrdinalIgnoreCase);

        // Case 2: Status = Disqualified
        await judgingService.ReviewEntryAsync(contest.Id, entry.Id, admin.Id, ContestEntryStatus.Disqualified, "Disqualified", "Rule breach");
        var formDisqualified = await entryService.GetEntryFormAsync(contest.Slug, author.Id);
        Assert.True(formDisqualified!.IsReadonly);
        var (success2, message2, _) = await entryService.SubmitOrUpdateEntryAsync(contest.Slug, author.Id, editModel);
        Assert.False(success2);
        Assert.Contains("can no longer be edited", message2, StringComparison.OrdinalIgnoreCase);

        // Case 3: Status = Selected
        using (var updateScope = fixture.Services.CreateScope())
        {
            var updateDb = updateScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var dbEntry = await updateDb.ContestEntries.FindAsync(entry.Id);
            dbEntry!.Status = ContestEntryStatus.Selected;
            await updateDb.SaveChangesAsync();
        }
        var formSelected = await entryService.GetEntryFormAsync(contest.Slug, author.Id);
        Assert.True(formSelected!.IsReadonly);
        var (success3, message3, _) = await entryService.SubmitOrUpdateEntryAsync(contest.Slug, author.Id, editModel);
        Assert.False(success3);
        Assert.Contains("can no longer be edited", message3, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Selected_winner_remains_strictly_private_until_admin_announces()
    {
        var (contest, entry, author) = await CreateContestWithEntryAsync(closesAtUtc: DateTime.UtcNow.AddDays(-1));

        using var scope = fixture.Services.CreateScope();
        var judgingService = scope.ServiceProvider.GetRequiredService<IContestJudgingService>();
        var contestService = scope.ServiceProvider.GetRequiredService<IContestService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var admin = await db.Users.FirstAsync();

        // Admin selects winner
        var (selectSuccess, _) = await judgingService.SelectWinnerAsync(contest.Id, entry.Id, admin.Id);
        Assert.True(selectSuccess);

        // Check public announcements: MUST NOT show unannounced winner
        var announcements = await judgingService.GetPublicAnnouncementsAsync();
        Assert.DoesNotContain(announcements, a => a.ContestId == contest.Id);

        // Check public contest detail: MUST NOT show winner banner
        var publicContest = await contestService.GetPublicContestBySlugAsync(contest.Slug);
        Assert.NotNull(publicContest);
        Assert.False(publicContest!.HasAnnouncedWinner);
        Assert.Null(publicContest.AnnouncedWinner);

        // Now test via HTTP client (Guest)
        var guestClient = fixture.NewClient();
        var publicDetailHtml = await guestClient.GetStringAsync($"/contests/{contest.Slug}");
        Assert.DoesNotContain("announcedWinnerBanner", publicDetailHtml);
        Assert.DoesNotContain(entry.Title, publicDetailHtml);
        Assert.DoesNotContain(author.DisplayName, publicDetailHtml);

        var publicAnnouncementsHtml = await guestClient.GetStringAsync("/announcements");
        Assert.DoesNotContain(entry.Title, publicAnnouncementsHtml);
        Assert.DoesNotContain(contest.Title, publicAnnouncementsHtml);
    }

    [Fact]
    public async Task Announced_winner_appears_on_public_pages_without_private_data_leakage()
    {
        var (contest, entry, author) = await CreateContestWithEntryAsync(closesAtUtc: DateTime.UtcNow.AddDays(-1));

        using var scope = fixture.Services.CreateScope();
        var judgingService = scope.ServiceProvider.GetRequiredService<IContestJudgingService>();
        var contestService = scope.ServiceProvider.GetRequiredService<IContestService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var admin = await db.Users.FirstAsync();

        // 1. Select Winner
        await judgingService.SelectWinnerAsync(contest.Id, entry.Id, admin.Id);

        // 2. Announce Winner
        var (announceSuccess, _) = await judgingService.AnnounceWinnerAsync(contest.Id, admin.Id);
        Assert.True(announceSuccess);

        // 3. Verify Public Announcements view model
        var announcements = await judgingService.GetPublicAnnouncementsAsync();
        var announcement = announcements.FirstOrDefault(a => a.ContestId == contest.Id);
        Assert.NotNull(announcement);
        Assert.Equal(entry.Title, announcement!.EntryTitle);
        Assert.Equal(author.DisplayName, announcement.WinnerDisplayName);
        Assert.Equal(contest.PrizeDescription, announcement.PrizeDescription);

        // 4. Verify Public Contest Detail view model
        var publicContest = await contestService.GetPublicContestBySlugAsync(contest.Slug);
        Assert.NotNull(publicContest);
        Assert.True(publicContest!.HasAnnouncedWinner);
        Assert.NotNull(publicContest.AnnouncedWinner);
        Assert.Equal(entry.Title, publicContest.AnnouncedWinner!.EntryTitle);
        Assert.Equal(author.DisplayName, publicContest.AnnouncedWinner.WinnerDisplayName);

        // 5. Verify Public HTTP pages and ensure no private data leakage
        var guestClient = fixture.NewClient();

        // /announcements
        var announcementsHtml = await guestClient.GetStringAsync("/announcements");
        Assert.Contains(contest.Title, announcementsHtml);
        Assert.Contains(entry.Title, announcementsHtml);
        Assert.Contains(author.DisplayName, announcementsHtml);
        // Privacy checks: ingredients, steps, and admin notes must NOT be present
        Assert.DoesNotContain("Secret Seasoning", announcementsHtml);
        Assert.DoesNotContain("Step 2: Bake at 200C", announcementsHtml);
        Assert.DoesNotContain(author.Email!, announcementsHtml);

        // /contests/{slug}
        var detailHtml = await guestClient.GetStringAsync($"/contests/{contest.Slug}");
        Assert.Contains("announcedWinnerBanner", detailHtml);
        Assert.Contains("Official Competition Winner", detailHtml);
        Assert.Contains(entry.Title, detailHtml);
        Assert.Contains(author.DisplayName, detailHtml);
        Assert.DoesNotContain("Secret Seasoning", detailHtml);
        Assert.DoesNotContain("Step 2: Bake at 200C", detailHtml);
        Assert.DoesNotContain(author.Email!, detailHtml);
    }

    [Fact]
    public async Task Admin_can_revoke_winner_with_reason_and_public_is_updated()
    {
        var (contest, entry, _) = await CreateContestWithEntryAsync(closesAtUtc: DateTime.UtcNow.AddDays(-1));

        using var scope = fixture.Services.CreateScope();
        var judgingService = scope.ServiceProvider.GetRequiredService<IContestJudgingService>();
        var contestService = scope.ServiceProvider.GetRequiredService<IContestService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var admin = await db.Users.FirstAsync();

        // Select and announce winner
        await judgingService.SelectWinnerAsync(contest.Id, entry.Id, admin.Id);
        await judgingService.AnnounceWinnerAsync(contest.Id, admin.Id);

        // Revoke winner
        const string revokeReason = "Winner voluntary withdrawal due to scheduling conflict.";
        var (revokeSuccess, _) = await judgingService.RevokeWinnerAsync(contest.Id, admin.Id, revokeReason);
        Assert.True(revokeSuccess);

        // Reload contest from DB
        var reloadedContest = await db.Contests.FindAsync(contest.Id);
        Assert.Null(reloadedContest!.WinningEntryId);
        Assert.Null(reloadedContest.WinnerSelectedAtUtc);
        Assert.Null(reloadedContest.WinnerAnnouncedAtUtc);

        // Reload entry from DB
        var reloadedEntry = await db.ContestEntries.FindAsync(entry.Id);
        Assert.Equal(ContestEntryStatus.UnderReview, reloadedEntry!.Status);
        Assert.NotNull(reloadedEntry.AdminReviewNotes);
        Assert.Contains(revokeReason, reloadedEntry.AdminReviewNotes);

        // Verify public announcements no longer includes contest
        var announcements = await judgingService.GetPublicAnnouncementsAsync();
        Assert.DoesNotContain(announcements, a => a.ContestId == contest.Id);

        // Verify public detail has no announced winner
        var publicContest = await contestService.GetPublicContestBySlugAsync(contest.Slug);
        Assert.False(publicContest!.HasAnnouncedWinner);
    }

    [Fact]
    public async Task Admin_can_change_winner_atomically()
    {
        var (contest, entry1, _) = await CreateContestWithEntryAsync(closesAtUtc: DateTime.UtcNow.AddDays(-1));

        // Create second entry for same contest
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var author2 = new ApplicationUser
        {
            UserName = NewEmail(),
            Email = NewEmail(),
            DisplayName = "Chef Bob",
            EmailConfirmed = true
        };
        db.Users.Add(author2);
        var entry2 = new ContestEntry
        {
            ContestId = contest.Id,
            AuthorUserId = author2.Id,
            Title = "Second Valid Dish",
            Summary = "Excellent dish",
            Status = ContestEntryStatus.Submitted,
            SubmittedAtUtc = DateTime.UtcNow.AddDays(-2)
        };
        db.ContestEntries.Add(entry2);
        await db.SaveChangesAsync();

        var judgingService = scope.ServiceProvider.GetRequiredService<IContestJudgingService>();
        var admin = await db.Users.FirstAsync();

        // Select entry1 as winner
        await judgingService.SelectWinnerAsync(contest.Id, entry1.Id, admin.Id);

        // Change winner to entry2
        var (changeSuccess, _) = await judgingService.SelectWinnerAsync(contest.Id, entry2.Id, admin.Id);
        Assert.True(changeSuccess);

        // Check DB state
        var reloadedContest = await db.Contests.FindAsync(contest.Id);
        Assert.Equal(entry2.Id, reloadedContest!.WinningEntryId);

        var reloaded1 = await db.ContestEntries.FindAsync(entry1.Id);
        var reloaded2 = await db.ContestEntries.FindAsync(entry2.Id);

        Assert.Equal(ContestEntryStatus.UnderReview, reloaded1!.Status);
        Assert.Equal(ContestEntryStatus.Selected, reloaded2!.Status);
    }

    [Fact]
    public async Task Database_filtered_unique_index_prevents_two_selected_winners_for_same_contest()
    {
        var (contest, entry1, _) = await CreateContestWithEntryAsync(closesAtUtc: DateTime.UtcNow.AddDays(-1));

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var author2 = new ApplicationUser
        {
            UserName = NewEmail(),
            Email = NewEmail(),
            DisplayName = "Chef Charlie",
            EmailConfirmed = true
        };
        db.Users.Add(author2);
        var entry2 = new ContestEntry
        {
            ContestId = contest.Id,
            AuthorUserId = author2.Id,
            Title = "Concurrent Dish",
            Summary = "Summary",
            Status = ContestEntryStatus.Submitted,
            SubmittedAtUtc = DateTime.UtcNow.AddDays(-2)
        };
        db.ContestEntries.Add(entry2);
        await db.SaveChangesAsync();

        // Force both entries to Selected directly on EF DbContext to trigger DB index constraint
        var e1 = await db.ContestEntries.FindAsync(entry1.Id);
        var e2 = await db.ContestEntries.FindAsync(entry2.Id);
        e1!.Status = ContestEntryStatus.Selected;
        e2!.Status = ContestEntryStatus.Selected;

        // SaveChanges should throw DbUpdateException due to filtered unique index IX_ContestEntries_ContestId_SingleWinner
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Non_admin_user_is_denied_from_all_judging_endpoints()
    {
        var (contest, entry, _) = await CreateContestWithEntryAsync(closesAtUtc: DateTime.UtcNow.AddDays(-1));

        var memberClient = fixture.NewClient();
        await RegisterAndLogin(memberClient, NewEmail(), NewPassword(), "Member Dan");

        // Attempt ReviewContestEntry
        var reviewRes = await memberClient.PostAsync($"/admin/contests/{contest.Id}/entries/{entry.Id}/review", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Status"] = ((int)ContestEntryStatus.UnderReview).ToString()
        }));
        Assert.True(reviewRes.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Redirect);

        // Attempt SelectContestWinner
        var selectRes = await memberClient.PostAsync($"/admin/contests/{contest.Id}/entries/{entry.Id}/select-winner", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ContestId"] = contest.Id.ToString()
        }));
        Assert.True(selectRes.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Redirect);

        // Attempt AnnounceContestWinner
        var announceRes = await memberClient.PostAsync($"/admin/contests/{contest.Id}/announce-winner", new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.True(announceRes.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Redirect);

        // Attempt RevokeContestWinner
        var revokeRes = await memberClient.PostAsync($"/admin/contests/{contest.Id}/revoke-winner", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Reason"] = "Unauthorized revocation"
        }));
        Assert.True(revokeRes.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task Full_admin_http_judging_flow_requires_antiforgery_tokens()
    {
        var (contest, entry, _) = await CreateContestWithEntryAsync(closesAtUtc: DateTime.UtcNow.AddDays(-1));
        var (adminClient, _) = await CreateAdminClientAsync();

        // POST without token -> 400 Bad Request
        var noTokenContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Status"] = ((int)ContestEntryStatus.UnderReview).ToString()
        });

        var res = await adminClient.PostAsync($"/admin/contests/{contest.Id}/entries/{entry.Id}/review", noTokenContent);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

        var selectNoToken = await adminClient.PostAsync($"/admin/contests/{contest.Id}/entries/{entry.Id}/select-winner", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["ContestId"] = contest.Id.ToString()
        }));
        Assert.Equal(HttpStatusCode.BadRequest, selectNoToken.StatusCode);
    }
}
