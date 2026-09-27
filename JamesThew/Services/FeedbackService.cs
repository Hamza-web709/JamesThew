using JamesThew.Data;
using JamesThew.Models;
using JamesThew.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace JamesThew.Services;

public class FeedbackService(ApplicationDbContext db) : IFeedbackService
{
    public async Task<bool> SubmitFeedbackAsync(string userId, FeedbackSubmitViewModel model)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(model.Message))
            return false;

        var feedback = new Feedback
        {
            AuthorUserId = userId,
            Kind = model.RecipeId.HasValue ? FeedbackKind.Recipe : model.Kind,
            RecipeId = model.RecipeId,
            Category = model.Category?.Trim(),
            Message = model.Message.Trim(),
            Rating = model.Rating,
            Status = FeedbackStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.Feedbacks.Add(feedback);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<IReadOnlyList<FeedbackHistoryItemDto>> GetUserFeedbackHistoryAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return [];

        return await db.Feedbacks
            .AsNoTracking()
            .Where(f => f.AuthorUserId == userId)
            .Include(f => f.Recipe)
                .ThenInclude(r => r!.ContentItem)
            .OrderByDescending(f => f.CreatedAtUtc)
            .Select(f => new FeedbackHistoryItemDto
            {
                Id = f.Id,
                Kind = f.Kind,
                RecipeTitle = f.Recipe != null ? f.Recipe.ContentItem.Title : null,
                Category = f.Category,
                Message = f.Message,
                Rating = f.Rating,
                Status = f.Status,
                AdminNotes = f.AdminNotes,
                CreatedAtUtc = f.CreatedAtUtc
            })
            .ToListAsync();
    }

    public async Task<AdminFeedbackListViewModel> GetAdminFeedbackListAsync(FeedbackStatus? statusFilter = null)
    {
        var query = db.Feedbacks
            .AsNoTracking()
            .Include(f => f.AuthorUser)
            .Include(f => f.Recipe)
                .ThenInclude(r => r!.ContentItem)
            .AsQueryable();

        if (statusFilter.HasValue)
        {
            query = query.Where(f => f.Status == statusFilter.Value);
        }

        var items = await query
            .OrderByDescending(f => f.CreatedAtUtc)
            .Select(f => new AdminFeedbackItemDto
            {
                Id = f.Id,
                AuthorUserId = f.AuthorUserId,
                AuthorDisplayName = f.AuthorUser != null ? f.AuthorUser.DisplayName : "Unknown",
                AuthorEmail = f.AuthorUser != null ? (f.AuthorUser.Email ?? "") : "",
                Kind = f.Kind,
                RecipeTitle = f.Recipe != null ? f.Recipe.ContentItem.Title : null,
                Category = f.Category,
                Message = f.Message,
                Rating = f.Rating,
                Status = f.Status,
                AdminNotes = f.AdminNotes,
                CreatedAtUtc = f.CreatedAtUtc
            })
            .ToListAsync();

        var total = await db.Feedbacks.CountAsync();
        var pending = await db.Feedbacks.CountAsync(f => f.Status == FeedbackStatus.Pending);

        return new AdminFeedbackListViewModel
        {
            Items = items,
            FilterStatus = statusFilter,
            TotalCount = total,
            PendingCount = pending
        };
    }

    public async Task<int> GetPendingFeedbackCountAsync()
    {
        return await db.Feedbacks.CountAsync(f => f.Status == FeedbackStatus.Pending);
    }

    public async Task<(bool Success, string Message)> ModerateFeedbackAsync(int id, FeedbackStatus newStatus, string? adminNotes)
    {
        var feedback = await db.Feedbacks.FirstOrDefaultAsync(f => f.Id == id);
        if (feedback is null)
            return (false, $"Feedback item #{id} was not found.");

        if (feedback.Status == newStatus)
            return (false, $"Feedback item #{id} is already in status {newStatus}.");

        feedback.Status = newStatus;
        if (!string.IsNullOrWhiteSpace(adminNotes))
        {
            var trimmedNotes = adminNotes.Trim();
            feedback.AdminNotes = trimmedNotes.Length > 500 ? trimmedNotes[..500] : trimmedNotes;
        }

        await db.SaveChangesAsync();
        return (true, $"Feedback item #{id} successfully marked as {newStatus}.");
    }
}
