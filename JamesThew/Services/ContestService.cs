using System.Text.RegularExpressions;
using JamesThew.Data;
using JamesThew.Models;
using JamesThew.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace JamesThew.Services;

public class ContestService(ApplicationDbContext db) : IContestService
{
    public async Task<PublicContestListViewModel> GetPublicContestsAsync(ContestTimelinePhase? phase = null, ContestType? type = null)
    {
        // Public visitors can view non-deleted published and closed contests (draft and archived remain private)
        var query = db.Contests
            .AsNoTracking()
            .Where(c => c.DeletedAtUtc == null && (c.Status == ContestStatus.Published || c.Status == ContestStatus.Closed));

        if (type.HasValue)
        {
            query = query.Where(c => c.Type == type.Value);
        }

        var allPublic = await query.ToListAsync();

        var totalPublic = allPublic.Count;
        var openNowCount = allPublic.Count(c => c.TimelinePhase == ContestTimelinePhase.Open);
        var upcomingCount = allPublic.Count(c => c.TimelinePhase == ContestTimelinePhase.Upcoming);
        var endedCount = allPublic.Count(c => c.TimelinePhase == ContestTimelinePhase.Ended);

        var filtered = allPublic.AsEnumerable();

        if (phase.HasValue)
        {
            filtered = filtered.Where(c => c.TimelinePhase == phase.Value);
        }

        // Order: Active Open first (ending soonest), then Upcoming (opening soonest), then Ended (most recent first)
        var ordered = filtered
            .OrderBy(c => c.TimelinePhase switch
            {
                ContestTimelinePhase.Open => 1,
                ContestTimelinePhase.Upcoming => 2,
                _ => 3
            })
            .ThenBy(c => c.TimelinePhase == ContestTimelinePhase.Open ? c.ClosesAtUtc : (c.TimelinePhase == ContestTimelinePhase.Upcoming ? c.OpensAtUtc : c.ClosesAtUtc))
            .Select(c => new ContestCardDto
            {
                Id = c.Id,
                Title = c.Title,
                Slug = c.Slug,
                Summary = c.Summary,
                Type = c.Type,
                Status = c.Status,
                TimelinePhase = c.TimelinePhase,
                OpensAtUtc = c.OpensAtUtc,
                ClosesAtUtc = c.ClosesAtUtc,
                ImageUrl = c.ImageUrl,
                PrizeDescription = c.PrizeDescription
            })
            .ToList();

        return new PublicContestListViewModel
        {
            Contests = ordered,
            PhaseFilter = phase,
            TypeFilter = type,
            TotalPublishedCount = totalPublic,
            OpenNowCount = openNowCount,
            UpcomingCount = upcomingCount,
            EndedCount = endedCount
        };
    }

    public async Task<ContestDetailViewModel?> GetPublicContestBySlugAsync(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return null;

        var contest = await db.Contests
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Slug == slug && c.DeletedAtUtc == null && (c.Status == ContestStatus.Published || c.Status == ContestStatus.Closed));

        if (contest == null)
            return null;

        return new ContestDetailViewModel
        {
            Id = contest.Id,
            Title = contest.Title,
            Slug = contest.Slug,
            Summary = contest.Summary,
            DescriptionAndRules = contest.DescriptionAndRules,
            Type = contest.Type,
            Status = contest.Status,
            TimelinePhase = contest.TimelinePhase,
            OpensAtUtc = contest.OpensAtUtc,
            ClosesAtUtc = contest.ClosesAtUtc,
            PrizeDescription = contest.PrizeDescription,
            ImageUrl = contest.ImageUrl,
            CreatedAtUtc = contest.CreatedAtUtc
        };
    }

    public async Task<AdminContestListViewModel> GetAdminContestsAsync(ContestStatus? status = null, ContestType? type = null, bool includeArchived = false)
    {
        var now = DateTime.UtcNow;

        var query = db.Contests.AsNoTracking();

        if (!includeArchived)
        {
            query = query.Where(c => c.DeletedAtUtc == null);
        }

        var allContests = await query.ToListAsync();

        var totalCount = allContests.Count(c => c.DeletedAtUtc == null);
        var publishedCount = allContests.Count(c => c.DeletedAtUtc == null && c.Status == ContestStatus.Published);
        var openNowCount = allContests.Count(c => c.DeletedAtUtc == null && c.Status == ContestStatus.Published && now >= c.OpensAtUtc && now <= c.ClosesAtUtc);
        var upcomingCount = allContests.Count(c => c.DeletedAtUtc == null && c.Status == ContestStatus.Published && now < c.OpensAtUtc);
        var draftsCount = allContests.Count(c => c.DeletedAtUtc == null && c.Status == ContestStatus.Draft);
        var closedCount = allContests.Count(c => c.DeletedAtUtc == null && c.Status == ContestStatus.Closed);
        var archivedCount = allContests.Count(c => c.DeletedAtUtc != null || c.Status == ContestStatus.Archived);

        var filtered = allContests.AsEnumerable();

        if (status.HasValue)
        {
            if (status.Value == ContestStatus.Archived)
            {
                filtered = filtered.Where(c => c.DeletedAtUtc != null || c.Status == ContestStatus.Archived);
            }
            else
            {
                filtered = filtered.Where(c => c.DeletedAtUtc == null && c.Status == status.Value);
            }
        }

        if (type.HasValue)
        {
            filtered = filtered.Where(c => c.Type == type.Value);
        }

        var ordered = filtered
            .OrderByDescending(c => c.CreatedAtUtc)
            .Select(c => new AdminContestRowDto
            {
                Id = c.Id,
                Title = c.Title,
                Slug = c.Slug,
                Type = c.Type,
                Status = c.Status,
                TimelinePhase = c.TimelinePhase,
                OpensAtUtc = c.OpensAtUtc,
                ClosesAtUtc = c.ClosesAtUtc,
                ImageUrl = c.ImageUrl,
                PrizeDescription = c.PrizeDescription,
                CreatedAtUtc = c.CreatedAtUtc,
                IsDeleted = c.DeletedAtUtc != null
            })
            .ToList();

        return new AdminContestListViewModel
        {
            Contests = ordered,
            StatusFilter = status,
            TypeFilter = type,
            TotalCount = totalCount,
            PublishedCount = publishedCount,
            OpenNowCount = openNowCount,
            UpcomingCount = upcomingCount,
            DraftsCount = draftsCount,
            ClosedCount = closedCount,
            ArchivedCount = archivedCount
        };
    }

    public async Task<AdminContestEditViewModel?> GetAdminContestByIdAsync(int id)
    {
        var contest = await db.Contests.FindAsync(id);
        if (contest == null)
            return null;

        return new AdminContestEditViewModel
        {
            Id = contest.Id,
            Title = contest.Title,
            Slug = contest.Slug,
            Summary = contest.Summary,
            DescriptionAndRules = contest.DescriptionAndRules,
            Type = contest.Type,
            Status = contest.Status,
            PrizeDescription = contest.PrizeDescription,
            ImageUrl = contest.ImageUrl,
            OpensAt = contest.OpensAtUtc,
            ClosesAt = contest.ClosesAtUtc
        };
    }

    public async Task<(bool Success, string Message, int? ContestId)> CreateContestAsync(AdminContestEditViewModel model, string adminUserId)
    {
        // 1. Date validation
        var opensUtc = EnsureUtc(model.OpensAt);
        var closesUtc = EnsureUtc(model.ClosesAt);

        if (closesUtc <= opensUtc)
        {
            return (false, "Closing date and time must be strictly later than the opening date and time.", null);
        }

        // 2. Slug generation & uniqueness
        var slug = await ResolveUniqueSlugAsync(model.Title, model.Slug, existingContestId: null);

        var contest = new Contest
        {
            Title = model.Title.Trim(),
            Slug = slug,
            Summary = model.Summary.Trim(),
            DescriptionAndRules = model.DescriptionAndRules.Trim(),
            Type = model.Type,
            Status = model.Status,
            PrizeDescription = string.IsNullOrWhiteSpace(model.PrizeDescription) ? null : model.PrizeDescription.Trim(),
            ImageUrl = string.IsNullOrWhiteSpace(model.ImageUrl) ? null : model.ImageUrl.Trim(),
            OpensAtUtc = opensUtc,
            ClosesAtUtc = closesUtc,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = adminUserId
        };

        db.Contests.Add(contest);
        await db.SaveChangesAsync();

        var stateLabel = contest.Status == ContestStatus.Published ? "published" : "saved as draft";
        return (true, $"Contest '{contest.Title}' successfully {stateLabel}.", contest.Id);
    }

    public async Task<(bool Success, string Message)> UpdateContestAsync(AdminContestEditViewModel model, string adminUserId)
    {
        var contest = await db.Contests.FindAsync(model.Id);
        if (contest == null)
        {
            return (false, $"Contest #{model.Id} was not found.");
        }

        // 1. Date validation
        var opensUtc = EnsureUtc(model.OpensAt);
        var closesUtc = EnsureUtc(model.ClosesAt);

        if (closesUtc <= opensUtc)
        {
            return (false, "Closing date and time must be strictly later than the opening date and time.");
        }

        // 2. Slug resolution
        var slug = await ResolveUniqueSlugAsync(model.Title, model.Slug, existingContestId: contest.Id);

        contest.Title = model.Title.Trim();
        contest.Slug = slug;
        contest.Summary = model.Summary.Trim();
        contest.DescriptionAndRules = model.DescriptionAndRules.Trim();
        contest.Type = model.Type;
        contest.Status = model.Status;
        contest.PrizeDescription = string.IsNullOrWhiteSpace(model.PrizeDescription) ? null : model.PrizeDescription.Trim();
        contest.ImageUrl = string.IsNullOrWhiteSpace(model.ImageUrl) ? null : model.ImageUrl.Trim();
        contest.OpensAtUtc = opensUtc;
        contest.ClosesAtUtc = closesUtc;
        contest.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return (true, $"Contest '{contest.Title}' updated successfully.");
    }

    public async Task<(bool Success, string Message)> ChangeStatusAsync(int id, ContestStatus targetStatus)
    {
        var contest = await db.Contests.FindAsync(id);
        if (contest == null)
        {
            return (false, $"Contest #{id} was not found.");
        }

        contest.Status = targetStatus;
        contest.UpdatedAtUtc = DateTime.UtcNow;
        if (targetStatus == ContestStatus.Archived)
        {
            contest.DeletedAtUtc = DateTime.UtcNow;
        }
        else
        {
            contest.DeletedAtUtc = null;
        }

        await db.SaveChangesAsync();

        var label = targetStatus switch
        {
            ContestStatus.Published => "published and is now visible to the public",
            ContestStatus.Draft => "moved to Draft status (hidden from public)",
            ContestStatus.Closed => "closed (no longer active for submissions)",
            ContestStatus.Archived => "archived",
            _ => targetStatus.ToString()
        };

        return (true, $"Contest '{contest.Title}' is now {label}.");
    }

    public async Task<(bool Success, string Message)> ArchiveContestAsync(int id)
    {
        var contest = await db.Contests.FindAsync(id);
        if (contest == null)
        {
            return (false, $"Contest #{id} was not found.");
        }

        contest.Status = ContestStatus.Archived;
        contest.DeletedAtUtc = DateTime.UtcNow;
        contest.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return (true, $"Contest '{contest.Title}' has been archived.");
    }

    public async Task<(bool Success, string Message)> RestoreContestAsync(int id)
    {
        var contest = await db.Contests.FindAsync(id);
        if (contest == null)
        {
            return (false, $"Contest #{id} was not found.");
        }

        contest.DeletedAtUtc = null;
        contest.Status = ContestStatus.Draft; // restore safely as draft
        contest.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return (true, $"Contest '{contest.Title}' restored to Draft state. You may edit or publish it when ready.");
    }

    public async Task<int> GetActiveOpenCountAsync()
    {
        var now = DateTime.UtcNow;
        return await db.Contests
            .AsNoTracking()
            .CountAsync(c => c.DeletedAtUtc == null &&
                             c.Status == ContestStatus.Published &&
                             now >= c.OpensAtUtc &&
                             now <= c.ClosesAtUtc);
    }

    public async Task<int> GetTotalContestsCountAsync()
    {
        return await db.Contests
            .AsNoTracking()
            .CountAsync(c => c.DeletedAtUtc == null);
    }

    private async Task<string> ResolveUniqueSlugAsync(string title, string? customSlug, int? existingContestId)
    {
        var baseSlug = !string.IsNullOrWhiteSpace(customSlug)
            ? GenerateKebabSlug(customSlug)
            : GenerateKebabSlug(title);

        if (string.IsNullOrWhiteSpace(baseSlug))
            baseSlug = "contest";

        if (baseSlug.Length > 140)
            baseSlug = baseSlug[..140].TrimEnd('-');

        var candidate = baseSlug;
        var suffix = 2;

        while (await db.Contests.AnyAsync(c => c.Slug == candidate && (!existingContestId.HasValue || c.Id != existingContestId.Value)))
        {
            candidate = $"{baseSlug}-{suffix}";
            suffix++;
        }

        return candidate;
    }

    private static string GenerateKebabSlug(string input)
    {
        var lower = input.ToLowerInvariant().Trim();
        var sanitized = Regex.Replace(lower, @"[^a-z0-9\s-]", "");
        var slug = Regex.Replace(sanitized, @"\s+", "-").Trim('-');
        return slug;
    }

    private static DateTime EnsureUtc(DateTime dt)
    {
        if (dt.Kind == DateTimeKind.Utc)
            return dt;

        if (dt.Kind == DateTimeKind.Unspecified)
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);

        return dt.ToUniversalTime();
    }
}
