using System.Text.RegularExpressions;
using JamesThew.Data;
using JamesThew.Models;
using JamesThew.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace JamesThew.Services;

public class ContributionService(ApplicationDbContext db) : IContributionService
{
    public async Task<bool> SubmitRecipeContributionAsync(string userId, string userDisplayName, RecipeContributionViewModel model)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(model.Title) || string.IsNullOrWhiteSpace(model.Summary))
            return false;

        var ingredientLines = (model.IngredientsText ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        var stepLines = (model.StepsText ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        if (ingredientLines.Count == 0 || stepLines.Count == 0)
            return false;

        var baseSlug = Regex.Replace(model.Title.ToLowerInvariant(), @"[^a-z0-9\s-]", "");
        baseSlug = Regex.Replace(baseSlug, @"\s+", "-").Trim('-');
        if (string.IsNullOrWhiteSpace(baseSlug))
            baseSlug = "recipe";
        var uniqueSlug = $"community-{baseSlug}-{Guid.NewGuid().ToString("N")[..6]}";

        var contentItem = new ContentItem
        {
            AuthorUserId = userId,
            AuthorDisplayName = string.IsNullOrWhiteSpace(userDisplayName) ? "Community Chef" : userDisplayName,
            Kind = ContentKind.Recipe,
            Origin = ContentOrigin.Community,
            Title = model.Title.Trim(),
            Slug = uniqueSlug,
            Summary = model.Summary.Trim(),
            Visibility = ContentVisibility.Free,
            PublicationStatus = PublicationStatus.Pending,
            ContributorNotes = model.Notes?.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        var recipe = new Recipe
        {
            ContentItem = contentItem,
            Servings = model.Servings,
            PrepMinutes = model.PrepMinutes,
            CookMinutes = model.CookMinutes
        };

        for (int i = 0; i < ingredientLines.Count; i++)
        {
            recipe.Ingredients.Add(new RecipeIngredient
            {
                Position = i + 1,
                Name = ingredientLines[i]
            });
        }

        for (int i = 0; i < stepLines.Count; i++)
        {
            recipe.Steps.Add(new RecipeStep
            {
                Position = i + 1,
                Instruction = stepLines[i]
            });
        }

        contentItem.Recipe = recipe;

        db.ContentItems.Add(contentItem);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SubmitTipContributionAsync(string userId, string userDisplayName, TipContributionViewModel model)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(model.Title) || string.IsNullOrWhiteSpace(model.Body))
            return false;

        var baseSlug = Regex.Replace(model.Title.ToLowerInvariant(), @"[^a-z0-9\s-]", "");
        baseSlug = Regex.Replace(baseSlug, @"\s+", "-").Trim('-');
        if (string.IsNullOrWhiteSpace(baseSlug))
            baseSlug = "tip";
        var uniqueSlug = $"community-{baseSlug}-{Guid.NewGuid().ToString("N")[..6]}";

        var contentItem = new ContentItem
        {
            AuthorUserId = userId,
            AuthorDisplayName = string.IsNullOrWhiteSpace(userDisplayName) ? "Community Chef" : userDisplayName,
            Kind = ContentKind.Tip,
            Origin = ContentOrigin.Community,
            Title = model.Title.Trim(),
            Slug = uniqueSlug,
            Summary = model.Summary.Trim(),
            Visibility = ContentVisibility.Free,
            PublicationStatus = PublicationStatus.Pending,
            ContributorNotes = model.Notes?.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
            Tip = new Tip
            {
                Body = model.Body.Trim()
            }
        };

        db.ContentItems.Add(contentItem);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<MemberContributionsIndexViewModel> GetMemberContributionsAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return new MemberContributionsIndexViewModel();

        var items = await db.ContentItems
            .AsNoTracking()
            .Where(c => c.AuthorUserId == userId && c.Origin == ContentOrigin.Community && c.DeletedAtUtc == null)
            .Include(c => c.Recipe)
            .OrderByDescending(c => c.CreatedAtUtc)
            .Select(c => new MemberContributionListItemDto
            {
                Id = c.Id,
                Kind = c.Kind,
                Title = c.Title,
                Slug = c.Slug,
                Summary = c.Summary,
                PublicationStatus = c.PublicationStatus,
                Visibility = c.Visibility,
                RejectionReason = c.RejectionReason,
                CreatedAtUtc = c.CreatedAtUtc,
                Servings = c.Recipe != null ? c.Recipe.Servings : null,
                PrepMinutes = c.Recipe != null ? c.Recipe.PrepMinutes : null,
                CookMinutes = c.Recipe != null ? c.Recipe.CookMinutes : null,
                Notes = c.ContributorNotes
            })
            .ToListAsync();

        return new MemberContributionsIndexViewModel
        {
            Items = items,
            TotalCount = items.Count,
            PendingCount = items.Count(x => x.PublicationStatus == PublicationStatus.Pending)
        };
    }

    public async Task<RecipeContributionViewModel?> GetRecipeContributionForEditAsync(int id, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId) || id <= 0)
            return null;

        var item = await db.ContentItems
            .AsNoTracking()
            .Include(c => c.Recipe)
                .ThenInclude(r => r!.Ingredients.OrderBy(i => i.Position))
            .Include(c => c.Recipe)
                .ThenInclude(r => r!.Steps.OrderBy(s => s.Position))
            .FirstOrDefaultAsync(c => c.Id == id && c.Origin == ContentOrigin.Community && c.DeletedAtUtc == null);

        if (item is null || item.AuthorUserId != userId || item.Kind != ContentKind.Recipe || item.Recipe is null)
            return null;

        var ingredientsText = string.Join(Environment.NewLine, item.Recipe.Ingredients.OrderBy(i => i.Position).Select(i => i.Name));
        var stepsText = string.Join(Environment.NewLine, item.Recipe.Steps.OrderBy(s => s.Position).Select(s => s.Instruction));

        return new RecipeContributionViewModel
        {
            Id = item.Id,
            Title = item.Title,
            Summary = item.Summary,
            Servings = item.Recipe.Servings,
            PrepMinutes = item.Recipe.PrepMinutes,
            CookMinutes = item.Recipe.CookMinutes,
            IngredientsText = ingredientsText,
            StepsText = stepsText,
            Notes = item.ContributorNotes,
            CurrentStatus = item.PublicationStatus,
            RejectionReason = item.RejectionReason
        };
    }

    public async Task<TipContributionViewModel?> GetTipContributionForEditAsync(int id, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId) || id <= 0)
            return null;

        var item = await db.ContentItems
            .AsNoTracking()
            .Include(c => c.Tip)
            .FirstOrDefaultAsync(c => c.Id == id && c.Origin == ContentOrigin.Community && c.DeletedAtUtc == null);

        if (item is null || item.AuthorUserId != userId || item.Kind != ContentKind.Tip || item.Tip is null)
            return null;

        return new TipContributionViewModel
        {
            Id = item.Id,
            Title = item.Title,
            Summary = item.Summary,
            Body = item.Tip.Body,
            Notes = item.ContributorNotes,
            CurrentStatus = item.PublicationStatus,
            RejectionReason = item.RejectionReason
        };
    }

    public async Task<(bool Success, string Message)> UpdateRecipeContributionAsync(int id, string userId, RecipeContributionViewModel model)
    {
        if (string.IsNullOrWhiteSpace(userId) || id <= 0)
            return (false, "Invalid contribution request.");

        var item = await db.ContentItems
            .Include(c => c.Recipe)
                .ThenInclude(r => r!.Ingredients)
            .Include(c => c.Recipe)
                .ThenInclude(r => r!.Steps)
            .FirstOrDefaultAsync(c => c.Id == id && c.Origin == ContentOrigin.Community && c.DeletedAtUtc == null);

        if (item is null || item.AuthorUserId != userId || item.Kind != ContentKind.Recipe || item.Recipe is null)
            return (false, "Contribution not found or unauthorized.");

        var ingredientLines = (model.IngredientsText ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        var stepLines = (model.StepsText ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        if (ingredientLines.Count == 0 || stepLines.Count == 0)
            return (false, "Please provide at least one ingredient and one preparation step.");

        item.Title = model.Title.Trim();
        item.Summary = model.Summary.Trim();
        item.ContributorNotes = model.Notes?.Trim();
        item.Recipe.Servings = model.Servings;
        item.Recipe.PrepMinutes = model.PrepMinutes;
        item.Recipe.CookMinutes = model.CookMinutes;
        item.UpdatedAtUtc = DateTime.UtcNow;

        item.Recipe.Ingredients.Clear();
        for (int i = 0; i < ingredientLines.Count; i++)
        {
            item.Recipe.Ingredients.Add(new RecipeIngredient
            {
                Position = i + 1,
                Name = ingredientLines[i]
            });
        }

        item.Recipe.Steps.Clear();
        for (int i = 0; i < stepLines.Count; i++)
        {
            item.Recipe.Steps.Add(new RecipeStep
            {
                Position = i + 1,
                Instruction = stepLines[i]
            });
        }

        // Re-review lifecycle rule:
        // Editing a contribution sets status back to Pending and clears rejection reasons.
        item.PublicationStatus = PublicationStatus.Pending;
        item.RejectionReason = null;

        await db.SaveChangesAsync();
        return (true, "Recipe contribution updated successfully and submitted for editorial review.");
    }

    public async Task<(bool Success, string Message)> UpdateTipContributionAsync(int id, string userId, TipContributionViewModel model)
    {
        if (string.IsNullOrWhiteSpace(userId) || id <= 0)
            return (false, "Invalid contribution request.");

        var item = await db.ContentItems
            .Include(c => c.Tip)
            .FirstOrDefaultAsync(c => c.Id == id && c.Origin == ContentOrigin.Community && c.DeletedAtUtc == null);

        if (item is null || item.AuthorUserId != userId || item.Kind != ContentKind.Tip || item.Tip is null)
            return (false, "Contribution not found or unauthorized.");

        if (string.IsNullOrWhiteSpace(model.Body) || model.Body.Trim().Length < 20)
            return (false, "Tip body instruction must be at least 20 characters.");

        item.Title = model.Title.Trim();
        item.Summary = model.Summary.Trim();
        item.ContributorNotes = model.Notes?.Trim();
        item.Tip.Body = model.Body.Trim();
        item.UpdatedAtUtc = DateTime.UtcNow;

        // Re-review lifecycle rule
        item.PublicationStatus = PublicationStatus.Pending;
        item.RejectionReason = null;

        await db.SaveChangesAsync();
        return (true, "Cooking tip contribution updated successfully and submitted for editorial review.");
    }

    public async Task<(bool Success, string Message)> DeleteContributionAsync(int id, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId) || id <= 0)
            return (false, "Invalid request.");

        var item = await db.ContentItems
            .FirstOrDefaultAsync(c => c.Id == id && c.Origin == ContentOrigin.Community && c.DeletedAtUtc == null);

        if (item is null || item.AuthorUserId != userId)
            return (false, "Contribution not found or unauthorized.");

        item.DeletedAtUtc = DateTime.UtcNow;
        item.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (true, "Contribution deleted successfully.");
    }

    public async Task<AdminContributionListViewModel> GetAdminContributionListAsync(ContentKind? kindFilter = null, PublicationStatus? statusFilter = null)
    {
        var query = db.ContentItems
            .AsNoTracking()
            .Where(c => c.Origin == ContentOrigin.Community && c.DeletedAtUtc == null)
            .Include(c => c.AuthorUser)
            .Include(c => c.Recipe)
                .ThenInclude(r => r!.Ingredients.OrderBy(i => i.Position))
            .Include(c => c.Recipe)
                .ThenInclude(r => r!.Steps.OrderBy(s => s.Position))
            .Include(c => c.Tip)
            .AsQueryable();

        if (kindFilter.HasValue)
        {
            query = query.Where(c => c.Kind == kindFilter.Value);
        }

        if (statusFilter.HasValue)
        {
            query = query.Where(c => c.PublicationStatus == statusFilter.Value);
        }

        var items = await query
            .OrderByDescending(c => c.CreatedAtUtc)
            .Select(c => new AdminContributionItemDto
            {
                Id = c.Id,
                Kind = c.Kind,
                Title = c.Title,
                Slug = c.Slug,
                Summary = c.Summary,
                AuthorDisplayName = c.AuthorDisplayName,
                AuthorEmail = c.AuthorUser != null ? (c.AuthorUser.Email ?? "") : "",
                PublicationStatus = c.PublicationStatus,
                Visibility = c.Visibility,
                RejectionReason = c.RejectionReason,
                CreatedAtUtc = c.CreatedAtUtc,
                UpdatedAtUtc = c.UpdatedAtUtc,
                Servings = c.Recipe != null ? c.Recipe.Servings : null,
                PrepMinutes = c.Recipe != null ? c.Recipe.PrepMinutes : null,
                CookMinutes = c.Recipe != null ? c.Recipe.CookMinutes : null,
                Ingredients = c.Recipe != null ? c.Recipe.Ingredients.OrderBy(i => i.Position).Select(i => i.Name).ToList() : new List<string>(),
                Steps = c.Recipe != null ? c.Recipe.Steps.OrderBy(s => s.Position).Select(s => s.Instruction).ToList() : new List<string>(),
                TipBody = c.Tip != null ? c.Tip.Body : null,
                Notes = c.ContributorNotes
            })
            .ToListAsync();

        var total = await db.ContentItems.CountAsync(c => c.Origin == ContentOrigin.Community && c.DeletedAtUtc == null);
        var pending = await db.ContentItems.CountAsync(c => c.Origin == ContentOrigin.Community && c.PublicationStatus == PublicationStatus.Pending && c.DeletedAtUtc == null);

        return new AdminContributionListViewModel
        {
            Items = items,
            FilterKind = kindFilter,
            FilterStatus = statusFilter,
            TotalCount = total,
            PendingCount = pending
        };
    }

    public async Task<int> GetPendingContributionCountAsync()
    {
        return await db.ContentItems.CountAsync(c => c.Origin == ContentOrigin.Community && c.PublicationStatus == PublicationStatus.Pending && c.DeletedAtUtc == null);
    }

    public async Task<(bool Success, string Message)> ApproveContributionAsync(int id, ContentVisibility visibility = ContentVisibility.Free)
    {
        var item = await db.ContentItems
            .FirstOrDefaultAsync(c => c.Id == id && c.Origin == ContentOrigin.Community && c.DeletedAtUtc == null);

        if (item is null)
            return (false, $"Contribution item #{id} was not found.");

        if (item.PublicationStatus == PublicationStatus.Published)
            return (false, $"Contribution '{item.Title}' is already published.");

        item.PublicationStatus = PublicationStatus.Published;
        item.Visibility = visibility;
        item.RejectionReason = null;
        item.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return (true, $"Contribution '{item.Title}' (#{id}) has been approved and published as {visibility}.");
    }

    public async Task<(bool Success, string Message)> RejectContributionAsync(int id, string? rejectionReason)
    {
        var item = await db.ContentItems
            .FirstOrDefaultAsync(c => c.Id == id && c.Origin == ContentOrigin.Community && c.DeletedAtUtc == null);

        if (item is null)
            return (false, $"Contribution item #{id} was not found.");

        if (item.PublicationStatus == PublicationStatus.Rejected)
            return (false, $"Contribution '{item.Title}' is already rejected.");

        var reason = string.IsNullOrWhiteSpace(rejectionReason)
            ? "Submission does not meet community editorial standards."
            : rejectionReason.Trim();

        item.PublicationStatus = PublicationStatus.Rejected;
        item.RejectionReason = reason.Length > 500 ? reason[..500] : reason;
        item.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return (true, $"Contribution '{item.Title}' (#{id}) has been rejected.");
    }
}
