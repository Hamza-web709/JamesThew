using System.ComponentModel.DataAnnotations;
using JamesThew.Models;

namespace JamesThew.ViewModels;

public class FeedbackSubmitViewModel
{
    [Required(ErrorMessage = "Please choose a feedback category.")]
    [StringLength(100)]
    public string Category { get; set; } = "General Culinary Feedback";

    [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5 stars.")]
    public int? Rating { get; set; }

    [Required(ErrorMessage = "Please enter your message.")]
    [StringLength(2000, MinimumLength = 10, ErrorMessage = "Feedback must be between 10 and 2000 characters.")]
    public string Message { get; set; } = string.Empty;

    public int? RecipeId { get; set; }
    public string? RecipeTitle { get; set; }

    public FeedbackKind Kind { get; set; } = FeedbackKind.Site;
}

public class FeedbackHistoryItemDto
{
    public int Id { get; set; }
    public FeedbackKind Kind { get; set; }
    public string? RecipeTitle { get; set; }
    public string? Category { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? Rating { get; set; }
    public FeedbackStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class FeedbackIndexViewModel
{
    public FeedbackSubmitViewModel Form { get; set; } = new();
    public IReadOnlyList<FeedbackHistoryItemDto> UserFeedbacks { get; set; } = [];
    public bool IsAuthenticated { get; set; }
    public string? UserDisplayName { get; set; }
}

public class AdminFeedbackListViewModel
{
    public IReadOnlyList<AdminFeedbackItemDto> Items { get; set; } = [];
    public FeedbackStatus? FilterStatus { get; set; }
    public int TotalCount { get; set; }
    public int PendingCount { get; set; }
}

public class AdminFeedbackItemDto
{
    public int Id { get; set; }
    public string AuthorUserId { get; set; } = string.Empty;
    public string AuthorDisplayName { get; set; } = string.Empty;
    public string AuthorEmail { get; set; } = string.Empty;
    public FeedbackKind Kind { get; set; }
    public string? RecipeTitle { get; set; }
    public string? Category { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? Rating { get; set; }
    public FeedbackStatus Status { get; set; }
    public string? AdminNotes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
