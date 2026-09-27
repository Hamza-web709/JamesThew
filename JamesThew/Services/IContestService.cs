using JamesThew.Models;
using JamesThew.ViewModels;

namespace JamesThew.Services;

public interface IContestService
{
    // Public queries
    Task<PublicContestListViewModel> GetPublicContestsAsync(ContestTimelinePhase? phase = null, ContestType? type = null);
    Task<ContestDetailViewModel?> GetPublicContestBySlugAsync(string slug);

    // Admin queries & operations
    Task<AdminContestListViewModel> GetAdminContestsAsync(ContestStatus? status = null, ContestType? type = null, bool includeArchived = false);
    Task<AdminContestEditViewModel?> GetAdminContestByIdAsync(int id);
    Task<(bool Success, string Message, int? ContestId)> CreateContestAsync(AdminContestEditViewModel model, string adminUserId);
    Task<(bool Success, string Message)> UpdateContestAsync(AdminContestEditViewModel model, string adminUserId);
    Task<(bool Success, string Message)> ChangeStatusAsync(int id, ContestStatus targetStatus);
    Task<(bool Success, string Message)> ArchiveContestAsync(int id);
    Task<(bool Success, string Message)> RestoreContestAsync(int id);
    Task<int> GetActiveOpenCountAsync();
    Task<int> GetTotalContestsCountAsync();
}
