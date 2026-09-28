using JamesThew.ViewModels;

namespace JamesThew.Services;

public interface IContestEntryService
{
    Task<ContestEntryFormViewModel?> GetEntryFormAsync(string contestSlug, string userId);
    Task<ContestEntryDetailViewModel?> GetMemberEntryDetailAsync(int entryId, string userId);
    Task<ContestEntryDetailViewModel?> GetMemberEntryByContestSlugAsync(string contestSlug, string userId);
    Task<(bool Success, string Message, int? EntryId)> SubmitOrUpdateEntryAsync(string contestSlug, string userId, ContestEntryFormViewModel model);
    Task<MyContestEntriesViewModel> GetMyEntriesAsync(string userId);
    Task<AdminContestEntriesViewModel?> GetAdminContestEntriesAsync(int contestId);
    Task<bool> HasMemberEnteredAsync(int contestId, string userId);
    Task<int?> GetMemberEntryIdAsync(int contestId, string userId);
    Task<int> GetEntryCountForContestAsync(int contestId);
}
