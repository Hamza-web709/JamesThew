using System.ComponentModel.DataAnnotations;
using JamesThew.Models;

namespace JamesThew.ViewModels;

public class ContestCardDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public ContestType Type { get; set; }
    public ContestStatus Status { get; set; }
    public ContestTimelinePhase TimelinePhase { get; set; }
    public DateTime OpensAtUtc { get; set; }
    public DateTime ClosesAtUtc { get; set; }
    public string? ImageUrl { get; set; }
    public string? PrizeDescription { get; set; }
}

public class PublicContestListViewModel
{
    public IReadOnlyList<ContestCardDto> Contests { get; set; } = [];
    public ContestTimelinePhase? PhaseFilter { get; set; }
    public ContestType? TypeFilter { get; set; }
    public int TotalPublishedCount { get; set; }
    public int OpenNowCount { get; set; }
    public int UpcomingCount { get; set; }
    public int EndedCount { get; set; }
}

public class ContestDetailViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string DescriptionAndRules { get; set; } = string.Empty;
    public ContestType Type { get; set; }
    public ContestStatus Status { get; set; }
    public ContestTimelinePhase TimelinePhase { get; set; }
    public DateTime OpensAtUtc { get; set; }
    public DateTime ClosesAtUtc { get; set; }
    public string? PrizeDescription { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public bool IsOpenForEntries => TimelinePhase == ContestTimelinePhase.Open && Status == ContestStatus.Published;
}

public class AdminContestRowDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public ContestType Type { get; set; }
    public ContestStatus Status { get; set; }
    public ContestTimelinePhase TimelinePhase { get; set; }
    public DateTime OpensAtUtc { get; set; }
    public DateTime ClosesAtUtc { get; set; }
    public string? ImageUrl { get; set; }
    public string? PrizeDescription { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
}

public class AdminContestListViewModel
{
    public IReadOnlyList<AdminContestRowDto> Contests { get; set; } = [];
    public ContestStatus? StatusFilter { get; set; }
    public ContestType? TypeFilter { get; set; }
    public int TotalCount { get; set; }
    public int PublishedCount { get; set; }
    public int OpenNowCount { get; set; }
    public int UpcomingCount { get; set; }
    public int DraftsCount { get; set; }
    public int ClosedCount { get; set; }
    public int ArchivedCount { get; set; }
}

public class AdminContestEditViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Contest title is required.")]
    [StringLength(150, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 150 characters.")]
    public string Title { get; set; } = string.Empty;

    [StringLength(160, ErrorMessage = "Custom slug cannot exceed 160 characters.")]
    [RegularExpression(@"^[a-z0-9-]+$", ErrorMessage = "Custom slug must contain only lowercase alphanumeric characters and hyphens.")]
    public string? Slug { get; set; }

    [Required(ErrorMessage = "Contest summary teaser is required.")]
    [StringLength(500, MinimumLength = 10, ErrorMessage = "Summary must be between 10 and 500 characters.")]
    public string Summary { get; set; } = string.Empty;

    [Required(ErrorMessage = "Contest description and rules are required.")]
    [StringLength(10000, MinimumLength = 20, ErrorMessage = "Description and rules must be between 20 and 10,000 characters.")]
    [Display(Name = "Description & Rules")]
    public string DescriptionAndRules { get; set; } = string.Empty;

    [Required]
    public ContestType Type { get; set; } = ContestType.Recipe;

    [Required]
    public ContestStatus Status { get; set; } = ContestStatus.Draft;

    [StringLength(500, ErrorMessage = "Prize description cannot exceed 500 characters.")]
    [Display(Name = "Prize / Recognition Details")]
    public string? PrizeDescription { get; set; }

    [StringLength(300, ErrorMessage = "Image URL cannot exceed 300 characters.")]
    [Display(Name = "Cover Image URL")]
    public string? ImageUrl { get; set; }

    [Required(ErrorMessage = "Opening date and time is required.")]
    [Display(Name = "Opens At (UTC)")]
    public DateTime OpensAt { get; set; } = DateTime.UtcNow;

    [Required(ErrorMessage = "Closing date and time is required.")]
    [Display(Name = "Closes At (UTC)")]
    public DateTime ClosesAt { get; set; } = DateTime.UtcNow.AddDays(30);

    public IReadOnlyList<MediaItemDto> AvailableMedia { get; set; } = [];
}
