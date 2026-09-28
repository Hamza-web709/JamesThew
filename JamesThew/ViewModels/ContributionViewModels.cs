using System.ComponentModel.DataAnnotations;
using JamesThew.Models;

namespace JamesThew.ViewModels;

public class RecipeContributionViewModel
{
    public int Id { get; set; }
    public PublicationStatus? CurrentStatus { get; set; }
    public string? RejectionReason { get; set; }

    [Required(ErrorMessage = "Recipe title is required.")]
    [StringLength(150, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 150 characters.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Recipe summary is required.")]
    [StringLength(500, MinimumLength = 10, ErrorMessage = "Summary must be between 10 and 500 characters.")]
    public string Summary { get; set; } = string.Empty;

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

    [StringLength(1000, ErrorMessage = "Contributor notes cannot exceed 1000 characters.")]
    public string? Notes { get; set; }
}

public class TipContributionViewModel
{
    public int Id { get; set; }
    public PublicationStatus? CurrentStatus { get; set; }
    public string? RejectionReason { get; set; }

    [Required(ErrorMessage = "Tip title is required.")]
    [StringLength(150, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 150 characters.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Summary is required.")]
    [StringLength(500, MinimumLength = 10, ErrorMessage = "Summary must be between 10 and 500 characters.")]
    public string Summary { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tip body instruction is required.")]
    [StringLength(6000, MinimumLength = 20, ErrorMessage = "Tip body must be between 20 and 6000 characters.")]
    public string Body { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Contributor notes cannot exceed 1000 characters.")]
    public string? Notes { get; set; }
}

public class MemberContributionListItemDto
{
    public int Id { get; set; }
    public ContentKind Kind { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public PublicationStatus PublicationStatus { get; set; }
    public ContentVisibility Visibility { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public int? Servings { get; set; }
    public int? PrepMinutes { get; set; }
    public int? CookMinutes { get; set; }
    public string? Notes { get; set; }
}

public class MemberContributionsIndexViewModel
{
    public IReadOnlyList<MemberContributionListItemDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int PendingCount { get; set; }
}

public class AdminContributionListViewModel
{
    public IReadOnlyList<AdminContributionItemDto> Items { get; set; } = [];
    public ContentKind? FilterKind { get; set; }
    public PublicationStatus? FilterStatus { get; set; }
    public int TotalCount { get; set; }
    public int PendingCount { get; set; }
}

public class AdminContributionItemDto
{
    public int Id { get; set; }
    public ContentKind Kind { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string AuthorDisplayName { get; set; } = string.Empty;
    public string AuthorEmail { get; set; } = string.Empty;
    public PublicationStatus PublicationStatus { get; set; }
    public ContentVisibility Visibility { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public int? Servings { get; set; }
    public int? PrepMinutes { get; set; }
    public int? CookMinutes { get; set; }
    public IReadOnlyList<string> Ingredients { get; set; } = [];
    public IReadOnlyList<string> Steps { get; set; } = [];
    public string? TipBody { get; set; }
    public string? Notes { get; set; }
}
