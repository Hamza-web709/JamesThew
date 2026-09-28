using JamesThew.Models;
using JamesThew.ViewModels;

namespace JamesThew.Services;

public interface IContestJudgingService
{
    Task<(bool Success, string Message)> ReviewEntryAsync(int contestId, int entryId, string adminUserId, ContestEntryStatus status, string? reviewNotes, string? disqualificationReason);
    Task<(bool Success, string Message)> SelectWinnerAsync(int contestId, int entryId, string adminUserId);
    Task<(bool Success, string Message)> AnnounceWinnerAsync(int contestId, string adminUserId);
    Task<(bool Success, string Message)> RevokeWinnerAsync(int contestId, string adminUserId, string? reason);
    Task<List<ContestWinnerAnnouncementDto>> GetPublicAnnouncementsAsync();
    Task<ContestWinnerAnnouncementDto?> GetAnnouncedWinnerForContestAsync(int contestId);
}
