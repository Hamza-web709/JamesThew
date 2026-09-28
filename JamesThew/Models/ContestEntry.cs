using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JamesThew.Models;

public class ContestEntry
{
    public int Id { get; set; }

    public int ContestId { get; set; }

    [ForeignKey(nameof(ContestId))]
    public Contest Contest { get; set; } = null!;

    [Required]
    public string AuthorUserId { get; set; } = string.Empty;

    [ForeignKey(nameof(AuthorUserId))]
    public ApplicationUser AuthorUser { get; set; } = null!;

    public ContestType EntryKind { get; set; }

    [Required, StringLength(150, MinimumLength = 3)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(500, MinimumLength = 10)]
    public string Summary { get; set; } = string.Empty;

    // Recipe specific fields
    public int? Servings { get; set; }
    public int? PrepMinutes { get; set; }
    public int? CookMinutes { get; set; }

    public ICollection<ContestEntryIngredient> Ingredients { get; set; } = new List<ContestEntryIngredient>();
    public ICollection<ContestEntryStep> Steps { get; set; } = new List<ContestEntryStep>();

    // Tip specific fields
    [StringLength(10000)]
    public string? TipBody { get; set; }

    // Common fields
    [StringLength(1000)]
    public string? ContributorNotes { get; set; }

    [StringLength(300)]
    public string? ImageUrl { get; set; }

    public ContestEntryStatus Status { get; set; } = ContestEntryStatus.Submitted;

    [StringLength(2000)]
    public string? AdminReviewNotes { get; set; }

    [StringLength(1000)]
    public string? DisqualificationReason { get; set; }

    [StringLength(1000)]
    public string? RevocationReason { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public DateTime? ReviewedAtUtc { get; set; }

    public string? ReviewedByUserId { get; set; }

    [ForeignKey(nameof(ReviewedByUserId))]
    public ApplicationUser? ReviewedByUser { get; set; }

    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }
}

public class ContestEntryIngredient
{
    public int Id { get; set; }

    public int ContestEntryId { get; set; }

    [ForeignKey(nameof(ContestEntryId))]
    public ContestEntry ContestEntry { get; set; } = null!;

    public int Position { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(50)]
    public string? QuantityText { get; set; }

    [StringLength(30)]
    public string? Unit { get; set; }
}

public class ContestEntryStep
{
    public int Id { get; set; }

    public int ContestEntryId { get; set; }

    [ForeignKey(nameof(ContestEntryId))]
    public ContestEntry ContestEntry { get; set; } = null!;

    public int Position { get; set; }

    [Required, StringLength(2000)]
    public string Instruction { get; set; } = string.Empty;
}
