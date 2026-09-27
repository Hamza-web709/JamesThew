using JamesThew.Data;
using JamesThew.Models;
using JamesThew.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace JamesThew.Services;

public class ContentService(ApplicationDbContext db) : IContentService
{
    public async Task<HomeViewModel> GetHomeDataAsync(bool canViewPaidContent)
    {
        // Fetch featured published recipes (Free recipes first for guests, followed by premium teasers).
        var recipeItems = await db.ContentItems
            .AsNoTracking()
            .Where(x => x.Kind == ContentKind.Recipe
                     && x.PublicationStatus == PublicationStatus.Published
                     && x.DeletedAtUtc == null)
            .Include(x => x.Recipe)
            .OrderBy(x => x.Visibility == ContentVisibility.Free ? 0 : 1)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Take(4)
            .Select(x => new RecipeCardViewModel
            {
                Id = x.Id,
                Title = x.Title,
                Slug = x.Slug,
                Summary = x.Summary,
                AuthorDisplayName = x.AuthorDisplayName,
                Visibility = x.Visibility,
                ImageUrl = x.ImageUrl,
                Servings = x.Recipe != null ? x.Recipe.Servings : null,
                PrepMinutes = x.Recipe != null ? x.Recipe.PrepMinutes : null,
                CookMinutes = x.Recipe != null ? x.Recipe.CookMinutes : null,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .ToListAsync();

        // Fetch featured published cooking tips.
        var tipItems = await db.ContentItems
            .AsNoTracking()
            .Where(x => x.Kind == ContentKind.Tip
                     && x.PublicationStatus == PublicationStatus.Published
                     && x.DeletedAtUtc == null)
            .OrderBy(x => x.Visibility == ContentVisibility.Free ? 0 : 1)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Take(3)
            .Select(x => new TipCardViewModel
            {
                Id = x.Id,
                Title = x.Title,
                Slug = x.Slug,
                Summary = x.Summary,
                AuthorDisplayName = x.AuthorDisplayName,
                Visibility = x.Visibility,
                ImageUrl = x.ImageUrl,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .ToListAsync();

        // Fetch top 4 quick FAQs for the home teaser section.
        var quickFaqs = await db.FaqItems
            .AsNoTracking()
            .Where(x => x.IsPublished)
            .OrderBy(x => x.SortOrder)
            .Take(4)
            .Select(x => new FaqItemViewModel
            {
                Id = x.Id,
                QuestionKey = x.QuestionKey,
                Question = x.Question,
                Answer = x.Answer,
                SortOrder = x.SortOrder
            })
            .ToListAsync();

        var totalRecipes = await db.ContentItems
            .AsNoTracking()
            .CountAsync(x => x.Kind == ContentKind.Recipe
                          && x.PublicationStatus == PublicationStatus.Published
                          && x.DeletedAtUtc == null);

        var totalTips = await db.ContentItems
            .AsNoTracking()
            .CountAsync(x => x.Kind == ContentKind.Tip
                          && x.PublicationStatus == PublicationStatus.Published
                          && x.DeletedAtUtc == null);

        return new HomeViewModel
        {
            FeaturedRecipes = recipeItems,
            FeaturedTips = tipItems,
            QuickFaqs = quickFaqs,
            TotalRecipesCount = totalRecipes,
            TotalTipsCount = totalTips
        };
    }

    public async Task<FaqViewModel> GetFaqsAsync()
    {
        var items = await db.FaqItems
            .AsNoTracking()
            .Where(x => x.IsPublished)
            .OrderBy(x => x.SortOrder)
            .Select(x => new FaqItemViewModel
            {
                Id = x.Id,
                QuestionKey = x.QuestionKey,
                Question = x.Question,
                Answer = x.Answer,
                SortOrder = x.SortOrder
            })
            .ToListAsync();

        return new FaqViewModel { Faqs = items };
    }

    public async Task<ContentSearchViewModel> SearchRecipesAsync(
        string? query, ContentVisibility? visibility, int page, int pageSize, bool canViewPaidContent)
    {
        return await SearchAllAsync(query, ContentKind.Recipe, visibility, page, pageSize, canViewPaidContent);
    }

    public async Task<ContentSearchViewModel> SearchTipsAsync(
        string? query, ContentVisibility? visibility, int page, int pageSize, bool canViewPaidContent)
    {
        return await SearchAllAsync(query, ContentKind.Tip, visibility, page, pageSize, canViewPaidContent);
    }

    public async Task<ContentSearchViewModel> SearchAllAsync(
        string? query, ContentKind? kind, ContentVisibility? visibility, int page, int pageSize, bool canViewPaidContent)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var trimmedQuery = query?.Trim();

        var baseQuery = db.ContentItems
            .AsNoTracking()
            .Where(x => x.PublicationStatus == PublicationStatus.Published && x.DeletedAtUtc == null);

        if (kind.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.Kind == kind.Value);
        }

        if (visibility.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.Visibility == visibility.Value);
        }

        // Apply authorized search filtering to prevent leaking protected content phrases to guests.
        if (!string.IsNullOrEmpty(trimmedQuery))
        {
            if (canViewPaidContent)
            {
                // Authorized users (Admins) can match titles, summaries, ingredients, steps, and tip bodies.
                baseQuery = baseQuery.Where(x =>
                    x.Title.Contains(trimmedQuery) ||
                    x.Summary.Contains(trimmedQuery) ||
                    (x.Recipe != null && x.Recipe.Ingredients.Any(i => i.Name.Contains(trimmedQuery))) ||
                    (x.Recipe != null && x.Recipe.Steps.Any(s => s.Instruction.Contains(trimmedQuery))) ||
                    (x.Tip != null && x.Tip.Body.Contains(trimmedQuery)));
            }
            else
            {
                // Guest / unauthorized users:
                // Can match Title and Summary of ANY item (so they know premium items exist).
                // But can ONLY search inside ingredients, steps, or tip bodies for Free items.
                // A search query for words inside a paid item's secret body will NEVER match.
                baseQuery = baseQuery.Where(x =>
                    x.Title.Contains(trimmedQuery) ||
                    x.Summary.Contains(trimmedQuery) ||
                    (x.Visibility == ContentVisibility.Free && x.Recipe != null && x.Recipe.Ingredients.Any(i => i.Name.Contains(trimmedQuery))) ||
                    (x.Visibility == ContentVisibility.Free && x.Recipe != null && x.Recipe.Steps.Any(s => s.Instruction.Contains(trimmedQuery))) ||
                    (x.Visibility == ContentVisibility.Free && x.Tip != null && x.Tip.Body.Contains(trimmedQuery)));
            }
        }

        var totalCount = await baseQuery.CountAsync();

        var pagedItems = await baseQuery
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(x => x.Recipe)
            .ToListAsync();

        var recipes = pagedItems
            .Where(x => x.Kind == ContentKind.Recipe)
            .Select(x => new RecipeCardViewModel
            {
                Id = x.Id,
                Title = x.Title,
                Slug = x.Slug,
                Summary = x.Summary,
                AuthorDisplayName = x.AuthorDisplayName,
                Visibility = x.Visibility,
                ImageUrl = x.ImageUrl,
                Servings = x.Recipe?.Servings,
                PrepMinutes = x.Recipe?.PrepMinutes,
                CookMinutes = x.Recipe?.CookMinutes,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .ToList();

        var tips = pagedItems
            .Where(x => x.Kind == ContentKind.Tip)
            .Select(x => new TipCardViewModel
            {
                Id = x.Id,
                Title = x.Title,
                Slug = x.Slug,
                Summary = x.Summary,
                AuthorDisplayName = x.AuthorDisplayName,
                Visibility = x.Visibility,
                ImageUrl = x.ImageUrl,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .ToList();

        return new ContentSearchViewModel
        {
            Query = trimmedQuery,
            Kind = kind,
            Visibility = visibility,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalCount,
            Recipes = recipes,
            Tips = tips
        };
    }

    public async Task<RecipeDetailViewModel?> GetRecipeBySlugAsync(string slug, bool canViewPaidContent)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return null;

        var item = await db.ContentItems
            .AsNoTracking()
            .Where(x => x.Slug == slug
                     && x.Kind == ContentKind.Recipe
                     && x.PublicationStatus == PublicationStatus.Published
                     && x.DeletedAtUtc == null)
            .Include(x => x.Recipe)
                .ThenInclude(r => r!.Ingredients.OrderBy(i => i.Position))
            .Include(x => x.Recipe)
                .ThenInclude(r => r!.Steps.OrderBy(s => s.Position))
            .FirstOrDefaultAsync();

        if (item is null)
            return null;

        var isLocked = item.Visibility == ContentVisibility.MembersOnly && !canViewPaidContent;

        var ingredients = isLocked
            ? (IReadOnlyList<RecipeIngredientItemViewModel>)[]
            : item.Recipe?.Ingredients
                .OrderBy(i => i.Position)
                .Select(i => new RecipeIngredientItemViewModel
                {
                    Position = i.Position,
                    Name = i.Name,
                    QuantityText = i.QuantityText,
                    Unit = i.Unit
                }).ToList() ?? [];

        var steps = isLocked
            ? (IReadOnlyList<RecipeStepItemViewModel>)[]
            : item.Recipe?.Steps
                .OrderBy(s => s.Position)
                .Select(s => new RecipeStepItemViewModel
                {
                    Position = s.Position,
                    Instruction = s.Instruction
                }).ToList() ?? [];

        return new RecipeDetailViewModel
        {
            Id = item.Id,
            Title = item.Title,
            Slug = item.Slug,
            Summary = item.Summary,
            AuthorDisplayName = item.AuthorDisplayName,
            Visibility = item.Visibility,
            IsLocked = isLocked,
            ImageUrl = item.ImageUrl,
            Servings = item.Recipe?.Servings,
            PrepMinutes = item.Recipe?.PrepMinutes,
            CookMinutes = item.Recipe?.CookMinutes,
            CreatedAtUtc = item.CreatedAtUtc,
            Ingredients = ingredients,
            Steps = steps
        };
    }

    public async Task<TipDetailViewModel?> GetTipBySlugAsync(string slug, bool canViewPaidContent)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return null;

        var item = await db.ContentItems
            .AsNoTracking()
            .Where(x => x.Slug == slug
                     && x.Kind == ContentKind.Tip
                     && x.PublicationStatus == PublicationStatus.Published
                     && x.DeletedAtUtc == null)
            .Include(x => x.Tip)
            .FirstOrDefaultAsync();

        if (item is null)
            return null;

        var isLocked = item.Visibility == ContentVisibility.MembersOnly && !canViewPaidContent;

        // Fetch 2-3 other tips for related reading
        var relatedTips = await db.ContentItems
            .AsNoTracking()
            .Where(x => x.Kind == ContentKind.Tip
                     && x.PublicationStatus == PublicationStatus.Published
                     && x.DeletedAtUtc == null
                     && x.Slug != slug)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(3)
            .Select(x => new TipCardViewModel
            {
                Id = x.Id,
                Title = x.Title,
                Slug = x.Slug,
                Summary = x.Summary,
                AuthorDisplayName = x.AuthorDisplayName,
                Visibility = x.Visibility,
                ImageUrl = x.ImageUrl,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .ToListAsync();

        return new TipDetailViewModel
        {
            Id = item.Id,
            Title = item.Title,
            Slug = item.Slug,
            Summary = item.Summary,
            AuthorDisplayName = item.AuthorDisplayName,
            Visibility = item.Visibility,
            IsLocked = isLocked,
            ImageUrl = item.ImageUrl,
            CreatedAtUtc = item.CreatedAtUtc,
            Body = isLocked ? null : item.Tip?.Body,
            RelatedTips = relatedTips
        };
    }
}
