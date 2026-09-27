using System.ComponentModel.DataAnnotations;
using JamesThew.Models;

namespace JamesThew.ViewModels;

public class AdminEditorialListViewModel
{
    public IReadOnlyList<AdminEditorialItemDto> Items { get; set; } = [];
    public ContentKind? FilterKind { get; set; }
    public ContentVisibility? FilterVisibility { get; set; }
    public PublicationStatus? FilterStatus { get; set; }
    public bool ShowDeleted { get; set; }
    public int TotalCount { get; set; }
    public int RecipesCount { get; set; }
    public int TipsCount { get; set; }
    public int PublishedCount { get; set; }
    public int DraftCount { get; set; }
}

public class AdminEditorialItemDto
{
    public int Id { get; set; }
    public ContentKind Kind { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public ContentVisibility Visibility { get; set; }
    public PublicationStatus PublicationStatus { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public int? Servings { get; set; }
    public int? PrepMinutes { get; set; }
    public int? CookMinutes { get; set; }
    public int IngredientsCount { get; set; }
    public int StepsCount { get; set; }
}

public class AdminRecipeEditViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Recipe title is required.")]
    [StringLength(150, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 150 characters.")]
    public string Title { get; set; } = string.Empty;

    [StringLength(160, ErrorMessage = "Slug cannot exceed 160 characters.")]
    public string? Slug { get; set; }

    [Required(ErrorMessage = "Recipe summary is required.")]
    [StringLength(500, MinimumLength = 10, ErrorMessage = "Summary must be between 10 and 500 characters.")]
    public string Summary { get; set; } = string.Empty;

    [StringLength(300, ErrorMessage = "Image URL cannot exceed 300 characters.")]
    public string? ImageUrl { get; set; }

    [Range(1, 100, ErrorMessage = "Servings must be between 1 and 100.")]
    public int? Servings { get; set; }

    [Range(0, 1440, ErrorMessage = "Prep minutes must be between 0 and 1440.")]
    public int? PrepMinutes { get; set; }

    [Range(0, 1440, ErrorMessage = "Cook minutes must be between 0 and 1440.")]
    public int? CookMinutes { get; set; }

    [Required(ErrorMessage = "Please provide the ingredients list (one per line).")]
    [StringLength(4000, MinimumLength = 5, ErrorMessage = "Ingredients must be between 5 and 4000 characters.")]
    public string IngredientsText { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please provide the preparation instructions (one step per line).")]
    [StringLength(6000, MinimumLength = 10, ErrorMessage = "Preparation steps must be between 10 and 6000 characters.")]
    public string StepsText { get; set; } = string.Empty;

    public ContentVisibility Visibility { get; set; } = ContentVisibility.Free;

    public PublicationStatus PublicationStatus { get; set; } = PublicationStatus.Published;

    public bool IsNew => Id == 0;
}

public class AdminTipEditViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Tip title is required.")]
    [StringLength(150, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 150 characters.")]
    public string Title { get; set; } = string.Empty;

    [StringLength(160, ErrorMessage = "Slug cannot exceed 160 characters.")]
    public string? Slug { get; set; }

    [Required(ErrorMessage = "Summary is required.")]
    [StringLength(500, MinimumLength = 10, ErrorMessage = "Summary must be between 10 and 500 characters.")]
    public string Summary { get; set; } = string.Empty;

    [StringLength(300, ErrorMessage = "Image URL cannot exceed 300 characters.")]
    public string? ImageUrl { get; set; }

    [Required(ErrorMessage = "Tip body instruction is required.")]
    [StringLength(6000, MinimumLength = 20, ErrorMessage = "Tip body must be between 20 and 6000 characters.")]
    public string Body { get; set; } = string.Empty;

    public ContentVisibility Visibility { get; set; } = ContentVisibility.Free;

    public PublicationStatus PublicationStatus { get; set; } = PublicationStatus.Published;

    public bool IsNew => Id == 0;
}
