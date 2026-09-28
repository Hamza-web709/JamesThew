using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JamesThew.Models;

public class Contest
{
    public int Id { get; set; }

    [Required, StringLength(150, MinimumLength = 3)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(160)]
    public string Slug { get; set; } = string.Empty;

    [Required, StringLength(500, MinimumLength = 10)]
    public string Summary { get; set; } = string.Empty;

    [Required, StringLength(10000, MinimumLength = 20)]
    public string DescriptionAndRules { get; set; } = string.Empty;

    public ContestType Type { get; set; } = ContestType.Recipe;

    public ContestStatus Status { get; set; } = ContestStatus.Draft;

    [StringLength(500)]
    public string? PrizeDescription { get; set; }

    [StringLength(300)]
    public string? ImageUrl { get; set; }

    public DateTime OpensAtUtc { get; set; }

    public DateTime ClosesAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public DateTime? DeletedAtUtc { get; set; }

    public string? CreatedByUserId { get; set; }

    [ForeignKey(nameof(CreatedByUserId))]
    public ApplicationUser? CreatedByUser { get; set; }

    public ICollection<ContestEntry> Entries { get; set; } = new List<ContestEntry>();

    public ContestTimelinePhase GetTimelinePhase(DateTime asOfUtc)
    {
        if (Status == ContestStatus.Closed) return ContestTimelinePhase.Ended;
        if (asOfUtc < OpensAtUtc) return ContestTimelinePhase.Upcoming;
        if (asOfUtc <= ClosesAtUtc) return ContestTimelinePhase.Open;
        return ContestTimelinePhase.Ended;
    }

    [NotMapped]
    public ContestTimelinePhase TimelinePhase => GetTimelinePhase(DateTime.UtcNow);

    [NotMapped]
    public bool IsActiveOpen => Status == ContestStatus.Published &&
                                DeletedAtUtc == null &&
                                TimelinePhase == ContestTimelinePhase.Open;
}
