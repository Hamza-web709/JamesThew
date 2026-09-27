namespace JamesThew.Models;

public enum FeedbackKind
{
    Site = 1,
    Recipe = 2
}

public enum FeedbackStatus
{
    Pending = 1,
    Reviewed = 2,
    Archived = 3
}

public class Feedback
{
    public int Id { get; set; }

    public string AuthorUserId { get; set; } = string.Empty;
    public ApplicationUser? AuthorUser { get; set; }

    public FeedbackKind Kind { get; set; } = FeedbackKind.Site;

    public int? RecipeId { get; set; }
    public Recipe? Recipe { get; set; }

    public string? Category { get; set; }

    public string Message { get; set; } = string.Empty;

    public int? Rating { get; set; }

    public FeedbackStatus Status { get; set; } = FeedbackStatus.Pending;

    public string? AdminNotes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
