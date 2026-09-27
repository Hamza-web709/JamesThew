namespace JamesThew.Models;

public class ContentItem
{
    public int Id { get; set; }

    public string? AuthorUserId { get; set; }
    public ApplicationUser? AuthorUser { get; set; }

    public string AuthorDisplayName { get; set; } = "James Thew";

    public ContentKind Kind { get; set; }
    public ContentOrigin Origin { get; set; } = ContentOrigin.Editorial;

    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;

    public ContentVisibility Visibility { get; set; } = ContentVisibility.Free;
    public PublicationStatus PublicationStatus { get; set; } = PublicationStatus.Published;

    public string? RejectionReason { get; set; }
    public string? ImageUrl { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public DateTime? DeletedAtUtc { get; set; }

    public Recipe? Recipe { get; set; }
    public Tip? Tip { get; set; }
}
