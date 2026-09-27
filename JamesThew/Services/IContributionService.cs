using JamesThew.Models;
using JamesThew.ViewModels;

namespace JamesThew.Services;

public interface IContributionService
{
    Task<bool> SubmitRecipeContributionAsync(string userId, string userDisplayName, RecipeContributionViewModel model);
    Task<bool> SubmitTipContributionAsync(string userId, string userDisplayName, TipContributionViewModel model);
    Task<MemberContributionsIndexViewModel> GetMemberContributionsAsync(string userId);
    Task<AdminContributionListViewModel> GetAdminContributionListAsync(ContentKind? kindFilter = null);
    Task<int> GetPendingContributionCountAsync();
}
