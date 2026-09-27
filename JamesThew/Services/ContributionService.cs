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

    public async Task<AdminContributionListViewModel> GetAdminContributionListAsync(ContentKind? kindFilter = null)
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
                CreatedAtUtc = c.CreatedAtUtc,
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
            TotalCount = total,
            PendingCount = pending
        };
    }

    public async Task<int> GetPendingContributionCountAsync()
    {
        return await db.ContentItems.CountAsync(c => c.Origin == ContentOrigin.Community && c.PublicationStatus == PublicationStatus.Pending && c.DeletedAtUtc == null);
    }
}
