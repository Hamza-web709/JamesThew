using JamesThew.Data;
using JamesThew.Models;
using JamesThew.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace JamesThew.Services;

public class ContestEntryService(ApplicationDbContext db) : IContestEntryService
{
    public async Task<ContestEntryFormViewModel?> GetEntryFormAsync(string contestSlug, string userId)
    {
        if (string.IsNullOrWhiteSpace(contestSlug))
            return null;

        var contest = await db.Contests
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Slug == contestSlug && c.DeletedAtUtc == null && c.Status != ContestStatus.Draft && c.Status != ContestStatus.Archived);

        if (contest is null)
            return null;

        var now = DateTime.UtcNow;
        var phase = contest.GetTimelinePhase(now);
        var isReadonly = phase != ContestTimelinePhase.Open || contest.Status == ContestStatus.Closed;

        ContestEntry? existing = null;
        if (!string.IsNullOrWhiteSpace(userId))
        {
            existing = await db.ContestEntries
                .AsNoTracking()
                .Include(e => e.Ingredients)
                .Include(e => e.Steps)
                .FirstOrDefaultAsync(e => e.ContestId == contest.Id && e.AuthorUserId == userId);
        }

        if (existing is not null)
        {
            if (existing.Status != ContestEntryStatus.Submitted)
            {
                isReadonly = true;
            }

            return new ContestEntryFormViewModel
            {
                ContestId = contest.Id,
                ContestTitle = contest.Title,
                ContestSlug = contest.Slug,
                ContestType = contest.Type,
                ClosesAtUtc = contest.ClosesAtUtc,
                EntryId = existing.Id,
                IsReadonly = isReadonly,
                Title = existing.Title,
                Summary = existing.Summary,
                Servings = existing.Servings,
                PrepMinutes = existing.PrepMinutes,
                CookMinutes = existing.CookMinutes,
                IngredientsText = string.Join("\n", existing.Ingredients.OrderBy(i => i.Position).Select(i => i.Name)),
                StepsText = string.Join("\n", existing.Steps.OrderBy(s => s.Position).Select(s => s.Instruction)),
                TipBody = existing.TipBody,
                Notes = existing.ContributorNotes,
                ImageUrl = existing.ImageUrl
            };
        }

        return new ContestEntryFormViewModel
        {
            ContestId = contest.Id,
            ContestTitle = contest.Title,
            ContestSlug = contest.Slug,
            ContestType = contest.Type,
            ClosesAtUtc = contest.ClosesAtUtc,
            EntryId = null,
            IsReadonly = isReadonly
        };
    }

    public async Task<ContestEntryDetailViewModel?> GetMemberEntryDetailAsync(int entryId, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId) || entryId <= 0)
            return null;

        var entry = await db.ContestEntries
            .AsNoTracking()
            .Include(e => e.Contest)
            .Include(e => e.Ingredients)
            .Include(e => e.Steps)
            .FirstOrDefaultAsync(e => e.Id == entryId && e.AuthorUserId == userId);

        if (entry is null)
            return null;

        var contest = entry.Contest;
        var now = DateTime.UtcNow;
        var phase = contest.GetTimelinePhase(now);
        var canEdit = phase == ContestTimelinePhase.Open &&
                      contest.Status == ContestStatus.Published &&
                      contest.DeletedAtUtc == null &&
                      entry.Status == ContestEntryStatus.Submitted;

        return new ContestEntryDetailViewModel
        {
            EntryId = entry.Id,
            ContestId = contest.Id,
            ContestTitle = contest.Title,
            ContestSlug = contest.Slug,
            ContestType = contest.Type,
            TimelinePhase = phase,
            ClosesAtUtc = contest.ClosesAtUtc,
            CanEdit = canEdit,
            Title = entry.Title,
            Summary = entry.Summary,
            Status = entry.Status,
            DisqualificationReason = entry.Status == ContestEntryStatus.Disqualified ? entry.DisqualificationReason : null,
            IsWinnerAnnounced = entry.Status == ContestEntryStatus.Selected && contest.WinnerAnnouncedAtUtc != null,
            SubmittedAtUtc = entry.SubmittedAtUtc,
            UpdatedAtUtc = entry.UpdatedAtUtc,
            ContributorNotes = entry.ContributorNotes,
            ImageUrl = entry.ImageUrl,
            Servings = entry.Servings,
            PrepMinutes = entry.PrepMinutes,
            CookMinutes = entry.CookMinutes,
            Ingredients = entry.Ingredients.OrderBy(i => i.Position).Select(i => i.Name).ToList(),
            Steps = entry.Steps.OrderBy(s => s.Position).Select(s => s.Instruction).ToList(),
            TipBody = entry.TipBody
        };
    }

    public async Task<ContestEntryDetailViewModel?> GetMemberEntryByContestSlugAsync(string contestSlug, string userId)
    {
        if (string.IsNullOrWhiteSpace(contestSlug) || string.IsNullOrWhiteSpace(userId))
            return null;

        var contest = await db.Contests
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Slug == contestSlug && c.DeletedAtUtc == null);

        if (contest is null)
            return null;

        var entry = await db.ContestEntries
            .AsNoTracking()
            .Include(e => e.Contest)
            .Include(e => e.Ingredients)
            .Include(e => e.Steps)
            .FirstOrDefaultAsync(e => e.ContestId == contest.Id && e.AuthorUserId == userId);

        if (entry is null)
            return null;

        return await GetMemberEntryDetailAsync(entry.Id, userId);
    }

    public async Task<(bool Success, string Message, int? EntryId)> SubmitOrUpdateEntryAsync(string contestSlug, string userId, ContestEntryFormViewModel model)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return (false, "You must be signed in as a member to submit a contest entry.", null);

        var contest = await db.Contests
            .FirstOrDefaultAsync(c => c.Slug == contestSlug && c.DeletedAtUtc == null);

        if (contest is null || contest.Status == ContestStatus.Draft || contest.Status == ContestStatus.Archived)
            return (false, "The specified contest could not be found or is not open to submissions.", null);

        var now = DateTime.UtcNow;

        if (contest.Status == ContestStatus.Closed)
            return (false, "Submissions for this competition have concluded.", null);

        if (now < contest.OpensAtUtc)
            return (false, $"Submissions for this competition open on {contest.OpensAtUtc:MMM dd, yyyy HH:mm} UTC.", null);

        if (now > contest.ClosesAtUtc)
            return (false, "Submissions for this competition have closed.", null);

        if (contest.Type != model.ContestType)
            return (false, $"Entry type mismatch. This competition accepts only {contest.Type} submissions.", null);

        var title = (model.Title ?? string.Empty).Trim();
        if (title.Length < 3 || title.Length > 150)
            return (false, "Entry title must be between 3 and 150 characters.", null);

        var summary = (model.Summary ?? string.Empty).Trim();
        if (summary.Length < 10 || summary.Length > 500)
            return (false, "Entry summary must be between 10 and 500 characters.", null);

        List<string> ingredientLines = [];
        List<string> stepLines = [];

        if (contest.Type == ContestType.Recipe)
        {
            ingredientLines = (model.IngredientsText ?? string.Empty)
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();

            stepLines = (model.StepsText ?? string.Empty)
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();

            if (ingredientLines.Count == 0)
                return (false, "Please provide at least one recipe ingredient.", null);

            if (stepLines.Count == 0)
                return (false, "Please provide at least one preparation step.", null);
        }
        else
        {
            var tipBody = (model.TipBody ?? string.Empty).Trim();
            if (tipBody.Length < 10 || tipBody.Length > 10000)
                return (false, "Tip technique instructions must be between 10 and 10,000 characters.", null);
        }

        var existing = await db.ContestEntries
            .Include(e => e.Ingredients)
            .Include(e => e.Steps)
            .FirstOrDefaultAsync(e => e.ContestId == contest.Id && e.AuthorUserId == userId);

        if (existing is not null)
        {
            // Edit existing entry
            if (now > contest.ClosesAtUtc || contest.Status == ContestStatus.Closed)
                return (false, "Contest submissions have closed. Edits are no longer permitted.", null);

            if (existing.Status != ContestEntryStatus.Submitted)
                return (false, "This entry is under evaluation or has been judged and can no longer be edited.", null);

            existing.Title = title;
            existing.Summary = summary;
            existing.ContributorNotes = model.Notes?.Trim();
            existing.ImageUrl = string.IsNullOrWhiteSpace(model.ImageUrl) ? null : model.ImageUrl.Trim();
            existing.UpdatedAtUtc = now;

            if (contest.Type == ContestType.Recipe)
            {
                existing.Servings = model.Servings;
                existing.PrepMinutes = model.PrepMinutes;
                existing.CookMinutes = model.CookMinutes;

                db.ContestEntryIngredients.RemoveRange(existing.Ingredients);
                db.ContestEntrySteps.RemoveRange(existing.Steps);

                for (int i = 0; i < ingredientLines.Count; i++)
                {
                    existing.Ingredients.Add(new ContestEntryIngredient
                    {
                        Position = i + 1,
                        Name = ingredientLines[i]
                    });
                }

                for (int i = 0; i < stepLines.Count; i++)
                {
                    existing.Steps.Add(new ContestEntryStep
                    {
                        Position = i + 1,
                        Instruction = stepLines[i]
                    });
                }
            }
            else
            {
                existing.TipBody = model.TipBody?.Trim();
            }

            await db.SaveChangesAsync();
            return (true, "Your contest entry has been updated successfully!", existing.Id);
        }
        else
        {
            // Create new entry
            var entry = new ContestEntry
            {
                ContestId = contest.Id,
                AuthorUserId = userId,
                EntryKind = contest.Type,
                Title = title,
                Summary = summary,
                ContributorNotes = model.Notes?.Trim(),
                ImageUrl = string.IsNullOrWhiteSpace(model.ImageUrl) ? null : model.ImageUrl.Trim(),
                Status = ContestEntryStatus.Submitted,
                SubmittedAtUtc = now
            };

            if (contest.Type == ContestType.Recipe)
            {
                entry.Servings = model.Servings;
                entry.PrepMinutes = model.PrepMinutes;
                entry.CookMinutes = model.CookMinutes;

                for (int i = 0; i < ingredientLines.Count; i++)
                {
                    entry.Ingredients.Add(new ContestEntryIngredient
                    {
                        Position = i + 1,
                        Name = ingredientLines[i]
                    });
                }

                for (int i = 0; i < stepLines.Count; i++)
                {
                    entry.Steps.Add(new ContestEntryStep
                    {
                        Position = i + 1,
                        Instruction = stepLines[i]
                    });
                }
            }
            else
            {
                entry.TipBody = model.TipBody?.Trim();
            }

            db.ContestEntries.Add(entry);
            try
            {
                await db.SaveChangesAsync();
                return (true, "Your contest entry has been submitted successfully!", entry.Id);
            }
            catch (DbUpdateException)
            {
                return (false, "An entry for this competition has already been submitted for your account.", null);
            }
        }
    }

    public async Task<MyContestEntriesViewModel> GetMyEntriesAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return new MyContestEntriesViewModel();

        var now = DateTime.UtcNow;

        var entries = await db.ContestEntries
            .AsNoTracking()
            .Include(e => e.Contest)
            .Where(e => e.AuthorUserId == userId)
            .OrderByDescending(e => e.SubmittedAtUtc)
            .Select(e => new MyContestEntryCardDto
            {
                EntryId = e.Id,
                ContestId = e.ContestId,
                ContestTitle = e.Contest.Title,
                ContestSlug = e.Contest.Slug,
                ContestType = e.Contest.Type,
                TimelinePhase = e.Contest.Status == ContestStatus.Closed
                    ? ContestTimelinePhase.Ended
                    : (now < e.Contest.OpensAtUtc
                        ? ContestTimelinePhase.Upcoming
                        : (now <= e.Contest.ClosesAtUtc ? ContestTimelinePhase.Open : ContestTimelinePhase.Ended)),
                ClosesAtUtc = e.Contest.ClosesAtUtc,
                CanEdit = e.Contest.Status == ContestStatus.Published &&
                          e.Contest.DeletedAtUtc == null &&
                          now <= e.Contest.ClosesAtUtc &&
                          now >= e.Contest.OpensAtUtc &&
                          e.Status == ContestEntryStatus.Submitted,
                EntryTitle = e.Title,
                Summary = e.Summary,
                Status = e.Status,
                DisqualificationReason = e.Status == ContestEntryStatus.Disqualified ? e.DisqualificationReason : null,
                IsWinnerAnnounced = e.Status == ContestEntryStatus.Selected && e.Contest.WinnerAnnouncedAtUtc != null,
                SubmittedAtUtc = e.SubmittedAtUtc,
                UpdatedAtUtc = e.UpdatedAtUtc,
                ImageUrl = e.ImageUrl
            })
            .ToListAsync();

        return new MyContestEntriesViewModel
        {
            Entries = entries,
            TotalCount = entries.Count
        };
    }

    public async Task<AdminContestEntriesViewModel?> GetAdminContestEntriesAsync(int contestId)
    {
        if (contestId <= 0)
            return null;

        var contest = await db.Contests
            .AsNoTracking()
            .Include(c => c.WinningEntry)
                .ThenInclude(w => w!.AuthorUser)
            .Include(c => c.WinnerSelectedByUser)
            .Include(c => c.WinnerAnnouncedByUser)
            .Include(c => c.Entries)
                .ThenInclude(e => e.AuthorUser)
            .Include(c => c.Entries)
                .ThenInclude(e => e.ReviewedByUser)
            .Include(c => c.Entries)
                .ThenInclude(e => e.Ingredients)
            .Include(c => c.Entries)
                .ThenInclude(e => e.Steps)
            .FirstOrDefaultAsync(c => c.Id == contestId);

        if (contest is null)
            return null;

        var rows = contest.Entries
            .OrderByDescending(e => e.SubmittedAtUtc)
            .Select(e => new AdminContestEntryRowDto
            {
                EntryId = e.Id,
                AuthorUserId = e.AuthorUserId,
                AuthorDisplayName = e.AuthorUser?.DisplayName ?? "Member",
                AuthorEmail = e.AuthorUser?.Email ?? "Unknown",
                Title = e.Title,
                Summary = e.Summary,
                Status = e.Status,
                AdminReviewNotes = e.AdminReviewNotes,
                DisqualificationReason = e.DisqualificationReason,
                RevocationReason = e.RevocationReason,
                RevokedAtUtc = e.RevokedAtUtc,
                ReviewedAtUtc = e.ReviewedAtUtc,
                ReviewedByDisplayName = e.ReviewedByUser?.DisplayName,
                IsSelectedWinner = contest.WinningEntryId == e.Id || e.Status == ContestEntryStatus.Selected,
                SubmittedAtUtc = e.SubmittedAtUtc,
                UpdatedAtUtc = e.UpdatedAtUtc,
                Notes = e.ContributorNotes,
                ImageUrl = e.ImageUrl,
                Servings = e.Servings,
                PrepMinutes = e.PrepMinutes,
                CookMinutes = e.CookMinutes,
                Ingredients = e.Ingredients.OrderBy(i => i.Position).Select(i => i.Name).ToList(),
                Steps = e.Steps.OrderBy(s => s.Position).Select(s => s.Instruction).ToList(),
                TipBody = e.TipBody
            })
            .ToList();

        var now = DateTime.UtcNow;
        var canSelectWinner = contest.Status != ContestStatus.Draft && contest.Status != ContestStatus.Archived && (now > contest.ClosesAtUtc || contest.Status == ContestStatus.Closed);
        return new AdminContestEntriesViewModel
        {
            ContestId = contest.Id,
            ContestTitle = contest.Title,
            ContestSlug = contest.Slug,
            ContestType = contest.Type,
            TimelinePhase = contest.GetTimelinePhase(now),
            ContestStatus = contest.Status,
            OpensAtUtc = contest.OpensAtUtc,
            ClosesAtUtc = contest.ClosesAtUtc,
            TotalEntries = rows.Count,
            WinningEntryId = contest.WinningEntryId,
            WinnerSelectedAtUtc = contest.WinnerSelectedAtUtc,
            WinnerSelectedByDisplayName = contest.WinnerSelectedByUser?.DisplayName,
            WinnerAnnouncedAtUtc = contest.WinnerAnnouncedAtUtc,
            WinnerAnnouncedByDisplayName = contest.WinnerAnnouncedByUser?.DisplayName,
            WinnerRevocationReason = contest.WinnerRevocationReason,
            WinnerRevokedAtUtc = contest.WinnerRevokedAtUtc,
            WinningEntryTitle = contest.WinningEntry?.Title,
            WinningAuthorDisplayName = contest.WinningEntry?.AuthorUser?.DisplayName,
            CanSelectWinner = canSelectWinner,
            Entries = rows
        };
    }

    public async Task<bool> HasMemberEnteredAsync(int contestId, string userId)
    {
        if (contestId <= 0 || string.IsNullOrWhiteSpace(userId))
            return false;

        return await db.ContestEntries
            .AsNoTracking()
            .AnyAsync(e => e.ContestId == contestId && e.AuthorUserId == userId);
    }

    public async Task<int?> GetMemberEntryIdAsync(int contestId, string userId)
    {
        if (contestId <= 0 || string.IsNullOrWhiteSpace(userId))
            return null;

        var entry = await db.ContestEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.ContestId == contestId && e.AuthorUserId == userId);

        return entry?.Id;
    }

    public async Task<int> GetEntryCountForContestAsync(int contestId)
    {
        if (contestId <= 0)
            return 0;

        return await db.ContestEntries
            .AsNoTracking()
            .CountAsync(e => e.ContestId == contestId);
    }
}
