using System.Text.RegularExpressions;
using JamesThew.Data;
using JamesThew.Models;
using JamesThew.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace JamesThew.Services;

public class AdminContentService(ApplicationDbContext db) : IAdminContentService
{
    public async Task<AdminEditorialListViewModel> GetEditorialContentListAsync(
        ContentKind? kind = null,
        ContentVisibility? visibility = null,
        PublicationStatus? status = null,
        bool includeDeleted = false)
    {
        var query = db.ContentItems
            .AsNoTracking()
            .Where(x => x.Origin == ContentOrigin.Editorial)
            .Include(x => x.Recipe)
                .ThenInclude(r => r!.Ingredients)
            .Include(x => x.Recipe)
                .ThenInclude(r => r!.Steps)
            .AsQueryable();

        if (!includeDeleted)
        {
            query = query.Where(x => x.DeletedAtUtc == null);
        }

        if (kind.HasValue)
        {
            query = query.Where(x => x.Kind == kind.Value);
        }

        if (visibility.HasValue)
        {
            query = query.Where(x => x.Visibility == visibility.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.PublicationStatus == status.Value);
        }

        var items = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new AdminEditorialItemDto
            {
                Id = x.Id,
                Kind = x.Kind,
                Title = x.Title,
                Slug = x.Slug,
                Summary = x.Summary,
                Visibility = x.Visibility,
                PublicationStatus = x.PublicationStatus,
                ImageUrl = x.ImageUrl,
                CreatedAtUtc = x.CreatedAtUtc,
                UpdatedAtUtc = x.UpdatedAtUtc,
                DeletedAtUtc = x.DeletedAtUtc,
                Servings = x.Recipe != null ? x.Recipe.Servings : null,
                PrepMinutes = x.Recipe != null ? x.Recipe.PrepMinutes : null,
                CookMinutes = x.Recipe != null ? x.Recipe.CookMinutes : null,
                IngredientsCount = x.Recipe != null ? x.Recipe.Ingredients.Count : 0,
                StepsCount = x.Recipe != null ? x.Recipe.Steps.Count : 0
            })
            .ToListAsync();

        var baseEditorialQuery = db.ContentItems.Where(x => x.Origin == ContentOrigin.Editorial);
        var total = await baseEditorialQuery.CountAsync();
        var recipes = await baseEditorialQuery.CountAsync(x => x.Kind == ContentKind.Recipe && x.DeletedAtUtc == null);
        var tips = await baseEditorialQuery.CountAsync(x => x.Kind == ContentKind.Tip && x.DeletedAtUtc == null);
        var published = await baseEditorialQuery.CountAsync(x => x.PublicationStatus == PublicationStatus.Published && x.DeletedAtUtc == null);
        var draft = await baseEditorialQuery.CountAsync(x => x.PublicationStatus == PublicationStatus.Draft && x.DeletedAtUtc == null);

        return new AdminEditorialListViewModel
        {
            Items = items,
            FilterKind = kind,
            FilterVisibility = visibility,
            FilterStatus = status,
            ShowDeleted = includeDeleted,
            TotalCount = total,
            RecipesCount = recipes,
            TipsCount = tips,
            PublishedCount = published,
            DraftCount = draft
        };
    }

    public async Task<AdminRecipeEditViewModel?> GetRecipeForEditAsync(int id)
    {
        var item = await db.ContentItems
            .AsNoTracking()
            .Where(x => x.Id == id && x.Kind == ContentKind.Recipe && x.Origin == ContentOrigin.Editorial)
            .Include(x => x.Recipe)
                .ThenInclude(r => r!.Ingredients.OrderBy(i => i.Position))
            .Include(x => x.Recipe)
                .ThenInclude(r => r!.Steps.OrderBy(s => s.Position))
            .FirstOrDefaultAsync();

        if (item is null || item.Recipe is null)
            return null;

        var ingredientsText = string.Join(Environment.NewLine, item.Recipe.Ingredients.OrderBy(i => i.Position).Select(i => i.Name));
        var stepsText = string.Join(Environment.NewLine, item.Recipe.Steps.OrderBy(s => s.Position).Select(s => s.Instruction));

        return new AdminRecipeEditViewModel
        {
            Id = item.Id,
            Title = item.Title,
            Slug = item.Slug,
            Summary = item.Summary,
            ImageUrl = item.ImageUrl,
            Servings = item.Recipe.Servings,
            PrepMinutes = item.Recipe.PrepMinutes,
            CookMinutes = item.Recipe.CookMinutes,
            IngredientsText = ingredientsText,
            StepsText = stepsText,
            Visibility = item.Visibility,
            PublicationStatus = item.PublicationStatus
        };
    }

    public async Task<AdminTipEditViewModel?> GetTipForEditAsync(int id)
    {
        var item = await db.ContentItems
            .AsNoTracking()
            .Where(x => x.Id == id && x.Kind == ContentKind.Tip && x.Origin == ContentOrigin.Editorial)
            .Include(x => x.Tip)
            .FirstOrDefaultAsync();

        if (item is null || item.Tip is null)
            return null;

        return new AdminTipEditViewModel
        {
            Id = item.Id,
            Title = item.Title,
            Slug = item.Slug,
            Summary = item.Summary,
            ImageUrl = item.ImageUrl,
            Body = item.Tip.Body,
            Visibility = item.Visibility,
            PublicationStatus = item.PublicationStatus
        };
    }

    public async Task<(bool Success, string Message, int Id, string Slug)> SaveRecipeAsync(
        AdminRecipeEditViewModel model,
        string adminUserId,
        string adminDisplayName)
    {
        if (string.IsNullOrWhiteSpace(model.Title) || string.IsNullOrWhiteSpace(model.Summary))
            return (false, "Recipe title and summary are required.", 0, string.Empty);

        var ingredientLines = (model.IngredientsText ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        var stepLines = (model.StepsText ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        if (ingredientLines.Count == 0 || stepLines.Count == 0)
            return (false, "A recipe must contain at least one ingredient and one preparation step.", 0, string.Empty);

        var uniqueSlug = await GenerateUniqueSlugAsync(model.Slug, model.Title, model.Id);

        ContentItem contentItem;
        Recipe recipe;

        if (model.Id == 0)
        {
            // CREATE new editorial recipe
            contentItem = new ContentItem
            {
                AuthorUserId = adminUserId,
                AuthorDisplayName = string.IsNullOrWhiteSpace(adminDisplayName) ? "James Thew" : adminDisplayName,
                Kind = ContentKind.Recipe,
                Origin = ContentOrigin.Editorial,
                Title = model.Title.Trim(),
                Slug = uniqueSlug,
                Summary = model.Summary.Trim(),
                ImageUrl = string.IsNullOrWhiteSpace(model.ImageUrl) ? null : model.ImageUrl.Trim(),
                Visibility = model.Visibility,
                PublicationStatus = model.PublicationStatus,
                CreatedAtUtc = DateTime.UtcNow
            };

            recipe = new Recipe
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
        }
        else
        {
            // EDIT existing editorial recipe
            var existing = await db.ContentItems
                .Include(x => x.Recipe)
                    .ThenInclude(r => r!.Ingredients)
                .Include(x => x.Recipe)
                    .ThenInclude(r => r!.Steps)
                .FirstOrDefaultAsync(x => x.Id == model.Id && x.Kind == ContentKind.Recipe && x.Origin == ContentOrigin.Editorial);

            if (existing is null || existing.Recipe is null)
                return (false, $"Editorial recipe #{model.Id} was not found.", 0, string.Empty);

            contentItem = existing;
            recipe = existing.Recipe;

            contentItem.Title = model.Title.Trim();
            contentItem.Slug = uniqueSlug;
            contentItem.Summary = model.Summary.Trim();
            contentItem.ImageUrl = string.IsNullOrWhiteSpace(model.ImageUrl) ? null : model.ImageUrl.Trim();
            contentItem.Visibility = model.Visibility;
            contentItem.PublicationStatus = model.PublicationStatus;
            contentItem.UpdatedAtUtc = DateTime.UtcNow;

            recipe.Servings = model.Servings;
            recipe.PrepMinutes = model.PrepMinutes;
            recipe.CookMinutes = model.CookMinutes;

            // Replace ingredients
            recipe.Ingredients.Clear();
            for (int i = 0; i < ingredientLines.Count; i++)
            {
                recipe.Ingredients.Add(new RecipeIngredient
                {
                    Position = i + 1,
                    Name = ingredientLines[i]
                });
            }

            // Replace steps
            recipe.Steps.Clear();
            for (int i = 0; i < stepLines.Count; i++)
            {
                recipe.Steps.Add(new RecipeStep
                {
                    Position = i + 1,
                    Instruction = stepLines[i]
                });
            }
        }

        await db.SaveChangesAsync();
        return (true, $"Recipe '{contentItem.Title}' saved successfully.", contentItem.Id, contentItem.Slug);
    }

    public async Task<(bool Success, string Message, int Id, string Slug)> SaveTipAsync(
        AdminTipEditViewModel model,
        string adminUserId,
        string adminDisplayName)
    {
        if (string.IsNullOrWhiteSpace(model.Title) || string.IsNullOrWhiteSpace(model.Summary) || string.IsNullOrWhiteSpace(model.Body))
            return (false, "Tip title, summary, and technique body are required.", 0, string.Empty);

        var uniqueSlug = await GenerateUniqueSlugAsync(model.Slug, model.Title, model.Id);

        ContentItem contentItem;

        if (model.Id == 0)
        {
            // CREATE new editorial tip
            contentItem = new ContentItem
            {
                AuthorUserId = adminUserId,
                AuthorDisplayName = string.IsNullOrWhiteSpace(adminDisplayName) ? "James Thew" : adminDisplayName,
                Kind = ContentKind.Tip,
                Origin = ContentOrigin.Editorial,
                Title = model.Title.Trim(),
                Slug = uniqueSlug,
                Summary = model.Summary.Trim(),
                ImageUrl = string.IsNullOrWhiteSpace(model.ImageUrl) ? null : model.ImageUrl.Trim(),
                Visibility = model.Visibility,
                PublicationStatus = model.PublicationStatus,
                CreatedAtUtc = DateTime.UtcNow,
                Tip = new Tip
                {
                    Body = model.Body.Trim()
                }
            };

            db.ContentItems.Add(contentItem);
        }
        else
        {
            // EDIT existing editorial tip
            var existing = await db.ContentItems
                .Include(x => x.Tip)
                .FirstOrDefaultAsync(x => x.Id == model.Id && x.Kind == ContentKind.Tip && x.Origin == ContentOrigin.Editorial);

            if (existing is null || existing.Tip is null)
                return (false, $"Editorial tip #{model.Id} was not found.", 0, string.Empty);

            contentItem = existing;
            contentItem.Title = model.Title.Trim();
            contentItem.Slug = uniqueSlug;
            contentItem.Summary = model.Summary.Trim();
            contentItem.ImageUrl = string.IsNullOrWhiteSpace(model.ImageUrl) ? null : model.ImageUrl.Trim();
            contentItem.Visibility = model.Visibility;
            contentItem.PublicationStatus = model.PublicationStatus;
            contentItem.UpdatedAtUtc = DateTime.UtcNow;

            existing.Tip.Body = model.Body.Trim();
        }

        await db.SaveChangesAsync();
        return (true, $"Cooking tip '{contentItem.Title}' saved successfully.", contentItem.Id, contentItem.Slug);
    }

    public async Task<(bool Success, string Message)> SoftDeleteContentAsync(int id)
    {
        var item = await db.ContentItems
            .FirstOrDefaultAsync(x => x.Id == id && x.Origin == ContentOrigin.Editorial);

        if (item is null)
            return (false, $"Editorial content item #{id} was not found.");

        if (item.DeletedAtUtc != null)
            return (false, $"Editorial content '{item.Title}' is already removed.");

        item.DeletedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (true, $"Editorial content '{item.Title}' (#{id}) has been removed/unpublished.");
    }

    public async Task<(bool Success, string Message)> RestoreContentAsync(int id)
    {
        var item = await db.ContentItems
            .FirstOrDefaultAsync(x => x.Id == id && x.Origin == ContentOrigin.Editorial);

        if (item is null)
            return (false, $"Editorial content item #{id} was not found.");

        if (item.DeletedAtUtc == null)
            return (false, $"Editorial content '{item.Title}' is not removed.");

        item.DeletedAtUtc = null;
        item.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (true, $"Editorial content '{item.Title}' (#{id}) has been restored.");
    }

    private async Task<string> GenerateUniqueSlugAsync(string? requestedSlug, string title, int currentId)
    {
        var rawSource = !string.IsNullOrWhiteSpace(requestedSlug) ? requestedSlug : title;
        var clean = Regex.Replace(rawSource.ToLowerInvariant(), @"[^a-z0-9\s-]", "");
        clean = Regex.Replace(clean, @"\s+", "-").Trim('-');

        if (string.IsNullOrWhiteSpace(clean))
            clean = "culinary-content";

        var candidate = clean;
        int counter = 1;

        while (await db.ContentItems.AnyAsync(x => x.Slug == candidate && x.Id != currentId))
        {
            counter++;
            candidate = $"{clean}-{counter}";
        }

        return candidate;
    }
}
