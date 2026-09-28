using System.ComponentModel.DataAnnotations;
using JamesThew.Models;

namespace JamesThew.ViewModels;

public class ContestWinnerAnnouncementDto
{
    public int ContestId { get; set; }
    public string ContestTitle { get; set; } = string.Empty;
    public string ContestSlug { get; set; } = string.Empty;
    public ContestType ContestType { get; set; }
    public string? PrizeDescription { get; set; }
    public int WinningEntryId { get; set; }
    public string EntryTitle { get; set; } = string.Empty;
    public string EntrySummary { get; set; } = string.Empty;
    public string? EntryImageUrl { get; set; }
    public string WinnerDisplayName { get; set; } = string.Empty;
    public DateTime AnnouncedAtUtc { get; set; }
}

public class ContestAnnouncementsViewModel
{
    public List<ContestWinnerAnnouncementDto> Announcements { get; set; } = [];
    public int TotalCount => Announcements.Count;
}

public class ReviewContestEntryInputModel
{
    [Required]
    public int ContestId { get; set; }

    [Required]
    public int EntryId { get; set; }

    [Required]
    public ContestEntryStatus Status { get; set; } = ContestEntryStatus.UnderReview;

    [StringLength(2000, ErrorMessage = "Review notes cannot exceed 2000 characters.")]
    [Display(Name = "Private Admin Review Notes")]
    public string? AdminReviewNotes { get; set; }

    [StringLength(1000, ErrorMessage = "Disqualification reason cannot exceed 1000 characters.")]
    [Display(Name = "Disqualification Reason")]
    public string? DisqualificationReason { get; set; }
}

public class SelectWinnerInputModel
{
    [Required]
    public int ContestId { get; set; }

    [Required]
    public int EntryId { get; set; }
}

public class RevokeWinnerInputModel
{
    [Required]
    public int ContestId { get; set; }

    [StringLength(500, ErrorMessage = "Revocation reason cannot exceed 500 characters.")]
    [Display(Name = "Reason for Revoking Winner")]
    public string? Reason { get; set; }
}
