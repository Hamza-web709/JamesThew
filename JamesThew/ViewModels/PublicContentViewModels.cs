using JamesThew.Models;

namespace JamesThew.ViewModels;

public class RecipeCardViewModel
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string AuthorDisplayName { get; init; } = "James Thew";
    public ContentVisibility Visibility { get; init; }
    public bool IsMembersOnly => Visibility == ContentVisibility.MembersOnly;
    public string? ImageUrl { get; init; }
    public int? Servings { get; init; }
    public int? PrepMinutes { get; init; }
    public int? CookMinutes { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public class TipCardViewModel
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string AuthorDisplayName { get; init; } = "James Thew";
    public ContentVisibility Visibility { get; init; }
    public bool IsMembersOnly => Visibility == ContentVisibility.MembersOnly;
    public string? ImageUrl { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public class RecipeDetailViewModel
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string AuthorDisplayName { get; init; } = "James Thew";
    public ContentVisibility Visibility { get; init; }
    public bool IsMembersOnly => Visibility == ContentVisibility.MembersOnly;
    public bool IsLocked { get; init; }
    public string? ImageUrl { get; init; }
    public int? Servings { get; init; }
    public int? PrepMinutes { get; init; }
    public int? CookMinutes { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public IReadOnlyList<RecipeIngredientItemViewModel> Ingredients { get; init; } = [];
    public IReadOnlyList<RecipeStepItemViewModel> Steps { get; init; } = [];
}

public class RecipeIngredientItemViewModel
{
    public int Position { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? QuantityText { get; init; }
    public string? Unit { get; init; }
}

public class RecipeStepItemViewModel
{
    public int Position { get; init; }
    public string Instruction { get; init; } = string.Empty;
}

public class TipDetailViewModel
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string AuthorDisplayName { get; init; } = "James Thew";
    public ContentVisibility Visibility { get; init; }
    public bool IsMembersOnly => Visibility == ContentVisibility.MembersOnly;
    public bool IsLocked { get; init; }
    public string? ImageUrl { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public string? Body { get; init; }
    public IReadOnlyList<TipCardViewModel> RelatedTips { get; init; } = [];
}

public class ContentSearchViewModel
{
    public string? Query { get; init; }
    public ContentKind? Kind { get; init; }
    public ContentVisibility? Visibility { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 12;
    public int TotalItems { get; init; }
    public int TotalPages => (int)Math.Ceiling((double)TotalItems / Math.Max(1, PageSize));
    public IReadOnlyList<RecipeCardViewModel> Recipes { get; init; } = [];
    public IReadOnlyList<TipCardViewModel> Tips { get; init; } = [];
}

public class HomeViewModel
{
    public IReadOnlyList<RecipeCardViewModel> FeaturedRecipes { get; init; } = [];
    public IReadOnlyList<TipCardViewModel> FeaturedTips { get; init; } = [];
    public IReadOnlyList<FaqItemViewModel> QuickFaqs { get; init; } = [];
    public int TotalRecipesCount { get; init; }
    public int TotalTipsCount { get; init; }
}

public class FaqViewModel
{
    public IReadOnlyList<FaqItemViewModel> Faqs { get; init; } = [];
}

public class FaqItemViewModel
{
    public int Id { get; init; }
    public string QuestionKey { get; init; } = string.Empty;
    public string Question { get; init; } = string.Empty;
    public string Answer { get; init; } = string.Empty;
    public int SortOrder { get; init; }
}
