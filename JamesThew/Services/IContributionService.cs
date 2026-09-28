using JamesThew.Models;
using JamesThew.ViewModels;

namespace JamesThew.Services;

public interface IContributionService
{
    Task<bool> SubmitRecipeContributionAsync(string userId, string userDisplayName, RecipeContributionViewModel model);
    Task<bool> SubmitTipContributionAsync(string userId, string userDisplayName, TipContributionViewModel model);
    Task<MemberContributionsIndexViewModel> GetMemberContributionsAsync(string userId);
    Task<RecipeContributionViewModel?> GetRecipeContributionForEditAsync(int id, string userId);
    Task<TipContributionViewModel?> GetTipContributionForEditAsync(int id, string userId);
    Task<(bool Success, string Message)> UpdateRecipeContributionAsync(int id, string userId, RecipeContributionViewModel model);
    Task<(bool Success, string Message)> UpdateTipContributionAsync(int id, string userId, TipContributionViewModel model);
    Task<(bool Success, string Message)> DeleteContributionAsync(int id, string userId);
    Task<AdminContributionListViewModel> GetAdminContributionListAsync(ContentKind? kindFilter = null, PublicationStatus? statusFilter = null);
    Task<int> GetPendingContributionCountAsync();
    Task<(bool Success, string Message)> ApproveContributionAsync(int id, ContentVisibility visibility = ContentVisibility.Free);
    Task<(bool Success, string Message)> RejectContributionAsync(int id, string? rejectionReason);
}
