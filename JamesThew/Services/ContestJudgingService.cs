using JamesThew.Data;
using JamesThew.Models;
using JamesThew.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace JamesThew.Services;

public class ContestJudgingService(ApplicationDbContext db) : IContestJudgingService
{
    public async Task<(bool Success, string Message)> ReviewEntryAsync(
        int contestId,
        int entryId,
        string adminUserId,
        ContestEntryStatus status,
        string? reviewNotes,
        string? disqualificationReason)
    {
        if (string.IsNullOrWhiteSpace(adminUserId))
            return (false, "Administrator identity is required.");

        var contest = await db.Contests
            .FirstOrDefaultAsync(c => c.Id == contestId && c.DeletedAtUtc == null);

        if (contest is null || contest.Status == ContestStatus.Draft || contest.Status == ContestStatus.Archived)
            return (false, "Competition not found or is in an invalid state for judging.");

        var entry = await db.ContestEntries
            .FirstOrDefaultAsync(e => e.Id == entryId && e.ContestId == contestId);

        if (entry is null)
            return (false, "Competition entry not found.");

        if (status == ContestEntryStatus.Selected)
        {
            return (false, "To designate an entry as the winner, use the formal Select Winner action.");
        }

        // If this entry was previously the selected winner and is now changed/disqualified, clear contest winner
        if (contest.WinningEntryId == entryId)
        {
            contest.WinningEntryId = null;
            contest.WinnerSelectedAtUtc = null;
            contest.WinnerSelectedByUserId = null;
            contest.WinnerAnnouncedAtUtc = null;
            contest.WinnerAnnouncedByUserId = null;
        }

        entry.Status = status;
        entry.AdminReviewNotes = string.IsNullOrWhiteSpace(reviewNotes) ? null : reviewNotes.Trim();
        if (status == ContestEntryStatus.Disqualified)
        {
            entry.DisqualificationReason = string.IsNullOrWhiteSpace(disqualificationReason)
                ? "Disqualified by culinary evaluation panel."
                : disqualificationReason.Trim();
        }
        else
        {
            entry.DisqualificationReason = null;
        }

        entry.ReviewedAtUtc = DateTime.UtcNow;
        entry.ReviewedByUserId = adminUserId;

        await db.SaveChangesAsync();
        return (true, "Entry evaluation updated successfully.");
    }

    public async Task<(bool Success, string Message)> SelectWinnerAsync(int contestId, int entryId, string adminUserId)
    {
        if (string.IsNullOrWhiteSpace(adminUserId))
            return (false, "Administrator identity is required.");

        var contest = await db.Contests
            .Include(c => c.Entries)
            .FirstOrDefaultAsync(c => c.Id == contestId && c.DeletedAtUtc == null);

        if (contest is null || contest.Status == ContestStatus.Draft || contest.Status == ContestStatus.Archived)
            return (false, "Competition not found or is in an invalid state for judging.");

        var now = DateTime.UtcNow;
        var isWindowClosed = now > contest.ClosesAtUtc || contest.Status == ContestStatus.Closed;
        if (!isWindowClosed)
        {
            return (false, "Winner cannot be selected while the competition submission window is still active.");
        }

        var entry = contest.Entries.FirstOrDefault(e => e.Id == entryId);
        if (entry is null)
        {
            return (false, "The selected entry does not belong to this competition.");
        }

        if (entry.Status == ContestEntryStatus.Disqualified)
        {
            return (false, "A disqualified entry cannot be selected as the winner.");
        }

        // Atomic transition: reset any previous winner's status to UnderReview
        foreach (var other in contest.Entries.Where(e => e.Id != entry.Id && e.Status == ContestEntryStatus.Selected))
        {
            other.Status = ContestEntryStatus.UnderReview;
        }

        entry.Status = ContestEntryStatus.Selected;
        entry.ReviewedAtUtc = now;
        entry.ReviewedByUserId = adminUserId;

        contest.WinningEntryId = entry.Id;
        contest.WinnerSelectedAtUtc = now;
        contest.WinnerSelectedByUserId = adminUserId;

        // Reset public announcement when winner changes; requires explicit re-announcement
        contest.WinnerAnnouncedAtUtc = null;
        contest.WinnerAnnouncedByUserId = null;

        await db.SaveChangesAsync();
        return (true, $"'{entry.Title}' has been selected as the official winner. Remember to announce the winner when ready.");
    }

    public async Task<(bool Success, string Message)> AnnounceWinnerAsync(int contestId, string adminUserId)
    {
        if (string.IsNullOrWhiteSpace(adminUserId))
            return (false, "Administrator identity is required.");

        var contest = await db.Contests
            .FirstOrDefaultAsync(c => c.Id == contestId && c.DeletedAtUtc == null);

        if (contest is null || contest.Status == ContestStatus.Draft || contest.Status == ContestStatus.Archived)
            return (false, "Competition not found or cannot be announced.");

        if (!contest.WinningEntryId.HasValue)
        {
            return (false, "Cannot announce winner before a winning entry has been selected.");
        }

        var now = DateTime.UtcNow;
        var isWindowClosed = now > contest.ClosesAtUtc || contest.Status == ContestStatus.Closed;
        if (!isWindowClosed)
        {
            return (false, "Cannot announce winner while competition submission window is still active.");
        }

        contest.WinnerAnnouncedAtUtc = now;
        contest.WinnerAnnouncedByUserId = adminUserId;

        await db.SaveChangesAsync();
        return (true, "Competition winner has been formally published to public announcements and the contest showcase.");
    }

    public async Task<(bool Success, string Message)> RevokeWinnerAsync(int contestId, string adminUserId, string? reason)
    {
        if (string.IsNullOrWhiteSpace(adminUserId))
            return (false, "Administrator identity is required.");

        var contest = await db.Contests
            .Include(c => c.Entries)
            .FirstOrDefaultAsync(c => c.Id == contestId && c.DeletedAtUtc == null);

        if (contest is null || contest.Status == ContestStatus.Draft || contest.Status == ContestStatus.Archived)
        {
            return (false, "Competition not found or cannot be modified.");
        }

        if (!contest.WinningEntryId.HasValue)
        {
            return (false, "This competition does not currently have a selected winner to revoke.");
        }

        var now = DateTime.UtcNow;
        var trimmedReason = reason?.Trim();
        var winningEntry = contest.Entries.FirstOrDefault(e => e.Id == contest.WinningEntryId.Value);
        if (winningEntry is not null)
        {
            winningEntry.Status = ContestEntryStatus.UnderReview;

            if (!string.IsNullOrWhiteSpace(trimmedReason))
            {
                var entryRevocationEntry = $"[{now:yyyy-MM-dd HH:mm} UTC]: {trimmedReason}";
                winningEntry.RevocationReason = string.IsNullOrWhiteSpace(winningEntry.RevocationReason)
                    ? entryRevocationEntry
                    : $"{winningEntry.RevocationReason}\n{entryRevocationEntry}";

                if (winningEntry.RevocationReason.Length > 1000)
                {
                    winningEntry.RevocationReason = winningEntry.RevocationReason.Substring(winningEntry.RevocationReason.Length - 1000);
                }

                var auditNote = $"[Winner Revoked by Admin on {now:yyyy-MM-dd HH:mm} UTC]: {trimmedReason}";
                winningEntry.AdminReviewNotes = string.IsNullOrWhiteSpace(winningEntry.AdminReviewNotes)
                    ? auditNote
                    : $"{winningEntry.AdminReviewNotes}\n{auditNote}";
            }

            winningEntry.RevokedAtUtc = now;
        }

        // Also reset any other entry with Status == Selected for consistency
        foreach (var other in contest.Entries.Where(e => e.Status == ContestEntryStatus.Selected))
        {
            other.Status = ContestEntryStatus.UnderReview;
        }

        contest.WinningEntryId = null;
        contest.WinnerSelectedAtUtc = null;
        contest.WinnerSelectedByUserId = null;
        contest.WinnerAnnouncedAtUtc = null;
        contest.WinnerAnnouncedByUserId = null;

        if (!string.IsNullOrWhiteSpace(trimmedReason))
        {
            var contestRevocationEntry = $"[{now:yyyy-MM-dd HH:mm} UTC, Entry #{winningEntry?.Id}]: {trimmedReason}";
            contest.WinnerRevocationReason = string.IsNullOrWhiteSpace(contest.WinnerRevocationReason)
                ? contestRevocationEntry
                : $"{contest.WinnerRevocationReason}\n{contestRevocationEntry}";

            if (contest.WinnerRevocationReason.Length > 1000)
            {
                contest.WinnerRevocationReason = contest.WinnerRevocationReason.Substring(contest.WinnerRevocationReason.Length - 1000);
            }
        }

        contest.WinnerRevokedAtUtc = now;
        contest.WinnerRevokedByUserId = adminUserId;

        await db.SaveChangesAsync();
        return (true, "Winner selection has been revoked successfully. Submissions returned to review.");
    }

    public async Task<List<ContestWinnerAnnouncementDto>> GetPublicAnnouncementsAsync()
    {
        return await db.Contests
            .AsNoTracking()
            .Where(c => c.DeletedAtUtc == null &&
                        c.Status != ContestStatus.Draft &&
                        c.Status != ContestStatus.Archived &&
                        c.WinningEntryId != null &&
                        c.WinnerAnnouncedAtUtc != null)
            .OrderByDescending(c => c.WinnerAnnouncedAtUtc)
            .Select(c => new ContestWinnerAnnouncementDto
            {
                ContestId = c.Id,
                ContestTitle = c.Title,
                ContestSlug = c.Slug,
                ContestType = c.Type,
                PrizeDescription = c.PrizeDescription,
                WinningEntryId = c.WinningEntryId!.Value,
                EntryTitle = c.WinningEntry!.Title,
                EntrySummary = c.WinningEntry.Summary,
                EntryImageUrl = c.WinningEntry.ImageUrl ?? c.ImageUrl,
                WinnerDisplayName = (c.WinningEntry.AuthorUser.DisplayName != null && c.WinningEntry.AuthorUser.DisplayName != "")
                    ? c.WinningEntry.AuthorUser.DisplayName
                    : "Culinary Member",
                AnnouncedAtUtc = c.WinnerAnnouncedAtUtc!.Value
            })
            .ToListAsync();
    }

    public async Task<ContestWinnerAnnouncementDto?> GetAnnouncedWinnerForContestAsync(int contestId)
    {
        if (contestId <= 0)
            return null;

        return await db.Contests
            .AsNoTracking()
            .Where(c => c.Id == contestId &&
                        c.DeletedAtUtc == null &&
                        c.Status != ContestStatus.Draft &&
                        c.Status != ContestStatus.Archived &&
                        c.WinningEntryId != null &&
                        c.WinnerAnnouncedAtUtc != null)
            .Select(c => new ContestWinnerAnnouncementDto
            {
                ContestId = c.Id,
                ContestTitle = c.Title,
                ContestSlug = c.Slug,
                ContestType = c.Type,
                PrizeDescription = c.PrizeDescription,
                WinningEntryId = c.WinningEntryId!.Value,
                EntryTitle = c.WinningEntry!.Title,
                EntrySummary = c.WinningEntry.Summary,
                EntryImageUrl = c.WinningEntry.ImageUrl ?? c.ImageUrl,
                WinnerDisplayName = (c.WinningEntry.AuthorUser.DisplayName != null && c.WinningEntry.AuthorUser.DisplayName != "")
                    ? c.WinningEntry.AuthorUser.DisplayName
                    : "Culinary Member",
                AnnouncedAtUtc = c.WinnerAnnouncedAtUtc!.Value
            })
            .FirstOrDefaultAsync();
    }
}
