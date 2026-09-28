using System.ComponentModel.DataAnnotations;
using JamesThew.Models;

namespace JamesThew.ViewModels;

public class ContestEntryFormViewModel
{
    public int ContestId { get; set; }
    public string ContestTitle { get; set; } = string.Empty;
    public string ContestSlug { get; set; } = string.Empty;
    public ContestType ContestType { get; set; }
    public DateTime ClosesAtUtc { get; set; }

    public int? EntryId { get; set; }
    public bool IsEdit => EntryId.HasValue && EntryId.Value > 0;
    public bool IsReadonly { get; set; }

    [Required(ErrorMessage = "Entry title is required.")]
    [StringLength(150, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 150 characters.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Summary teaser is required.")]
    [StringLength(500, MinimumLength = 10, ErrorMessage = "Summary must be between 10 and 500 characters.")]
    public string Summary { get; set; } = string.Empty;

    // Recipe specific fields
    [Range(1, 100, ErrorMessage = "Servings must be between 1 and 100.")]
    public int? Servings { get; set; }

    [Range(0, 1440, ErrorMessage = "Prep time must be between 0 and 1440 minutes.")]
    [Display(Name = "Prep Time (minutes)")]
    public int? PrepMinutes { get; set; }

    [Range(0, 1440, ErrorMessage = "Cook time must be between 0 and 1440 minutes.")]
    [Display(Name = "Cook Time (minutes)")]
    public int? CookMinutes { get; set; }

    [Display(Name = "Ingredients (one item per line)")]
    public string? IngredientsText { get; set; }

    [Display(Name = "Preparation Steps (one step per line)")]
    public string? StepsText { get; set; }

    // Tip specific fields
    [StringLength(10000, MinimumLength = 10, ErrorMessage = "Tip technique must be between 10 and 10,000 characters.")]
    [Display(Name = "Culinary Technique & Instructions")]
    public string? TipBody { get; set; }

    // Common fields
    [StringLength(1000, ErrorMessage = "Notes cannot exceed 1000 characters.")]
    [Display(Name = "Author Remarks / Culinary Context (optional)")]
    public string? Notes { get; set; }

    [StringLength(300, ErrorMessage = "Image URL cannot exceed 300 characters.")]
    [Display(Name = "Image URL (optional)")]
    public string? ImageUrl { get; set; }
}

public class MyContestEntryCardDto
{
    public int EntryId { get; set; }
    public int ContestId { get; set; }
    public string ContestTitle { get; set; } = string.Empty;
    public string ContestSlug { get; set; } = string.Empty;
    public ContestType ContestType { get; set; }
    public ContestTimelinePhase TimelinePhase { get; set; }
    public DateTime ClosesAtUtc { get; set; }
    public bool CanEdit { get; set; }

    public string EntryTitle { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public ContestEntryStatus Status { get; set; }
    public string? DisqualificationReason { get; set; }
    public bool IsWinner => Status == ContestEntryStatus.Selected;
    public bool IsWinnerAnnounced { get; set; }
    public DateTime SubmittedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? ImageUrl { get; set; }
}

public class MyContestEntriesViewModel
{
    public IReadOnlyList<MyContestEntryCardDto> Entries { get; set; } = [];
    public int TotalCount { get; set; }
}

public class ContestEntryDetailViewModel
{
    public int EntryId { get; set; }
    public int ContestId { get; set; }
    public string ContestTitle { get; set; } = string.Empty;
    public string ContestSlug { get; set; } = string.Empty;
    public ContestType ContestType { get; set; }
    public ContestTimelinePhase TimelinePhase { get; set; }
    public DateTime ClosesAtUtc { get; set; }
    public bool CanEdit { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public ContestEntryStatus Status { get; set; }
    public string? DisqualificationReason { get; set; }
    public bool IsWinner => Status == ContestEntryStatus.Selected;
    public bool IsWinnerAnnounced { get; set; }
    public DateTime SubmittedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? ContributorNotes { get; set; }
    public string? ImageUrl { get; set; }

    // Recipe details
    public int? Servings { get; set; }
    public int? PrepMinutes { get; set; }
    public int? CookMinutes { get; set; }
    public IReadOnlyList<string> Ingredients { get; set; } = [];
    public IReadOnlyList<string> Steps { get; set; } = [];

    // Tip details
    public string? TipBody { get; set; }
}

public class AdminContestEntryRowDto
{
    public int EntryId { get; set; }
    public string AuthorUserId { get; set; } = string.Empty;
    public string AuthorDisplayName { get; set; } = string.Empty;
    public string AuthorEmail { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public ContestEntryStatus Status { get; set; }
    public string? AdminReviewNotes { get; set; }
    public string? DisqualificationReason { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewedByDisplayName { get; set; }
    public bool IsSelectedWinner { get; set; }
    public DateTime SubmittedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? Notes { get; set; }
    public string? ImageUrl { get; set; }

    // Recipe details
    public int? Servings { get; set; }
    public int? PrepMinutes { get; set; }
    public int? CookMinutes { get; set; }
    public IReadOnlyList<string> Ingredients { get; set; } = [];
    public IReadOnlyList<string> Steps { get; set; } = [];

    // Tip details
    public string? TipBody { get; set; }
}

public class AdminContestEntriesViewModel
{
    public int ContestId { get; set; }
    public string ContestTitle { get; set; } = string.Empty;
    public string ContestSlug { get; set; } = string.Empty;
    public ContestType ContestType { get; set; }
    public ContestTimelinePhase TimelinePhase { get; set; }
    public ContestStatus ContestStatus { get; set; }
    public DateTime OpensAtUtc { get; set; }
    public DateTime ClosesAtUtc { get; set; }
    public int TotalEntries { get; set; }
    public int? WinningEntryId { get; set; }
    public DateTime? WinnerSelectedAtUtc { get; set; }
    public string? WinnerSelectedByDisplayName { get; set; }
    public DateTime? WinnerAnnouncedAtUtc { get; set; }
    public string? WinnerAnnouncedByDisplayName { get; set; }
    public string? WinningEntryTitle { get; set; }
    public string? WinningAuthorDisplayName { get; set; }
    public bool CanSelectWinner { get; set; }
    public bool HasSelectedWinner => WinningEntryId.HasValue;
    public bool HasAnnouncedWinner => WinningEntryId.HasValue && WinnerAnnouncedAtUtc.HasValue;
    public IReadOnlyList<AdminContestEntryRowDto> Entries { get; set; } = [];
}
