using JamesThew.Models;
using JamesThew.ViewModels;

namespace JamesThew.Services;

public interface IFeedbackService
{
    Task<bool> SubmitFeedbackAsync(string userId, FeedbackSubmitViewModel model);
    Task<IReadOnlyList<FeedbackHistoryItemDto>> GetUserFeedbackHistoryAsync(string userId);
    Task<AdminFeedbackListViewModel> GetAdminFeedbackListAsync(FeedbackStatus? statusFilter = null);
    Task<int> GetPendingFeedbackCountAsync();
}
