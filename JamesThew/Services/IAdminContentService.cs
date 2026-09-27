using JamesThew.Models;
using JamesThew.ViewModels;

namespace JamesThew.Services;

public interface IAdminContentService
{
    Task<AdminEditorialListViewModel> GetEditorialContentListAsync(
        ContentKind? kind = null,
        ContentVisibility? visibility = null,
        PublicationStatus? status = null,
        bool includeDeleted = false);

    Task<AdminRecipeEditViewModel?> GetRecipeForEditAsync(int id);

    Task<AdminTipEditViewModel?> GetTipForEditAsync(int id);

    Task<(bool Success, string Message, int Id, string Slug)> SaveRecipeAsync(
        AdminRecipeEditViewModel model,
        string adminUserId,
        string adminDisplayName);

    Task<(bool Success, string Message, int Id, string Slug)> SaveTipAsync(
        AdminTipEditViewModel model,
        string adminUserId,
        string adminDisplayName);

    Task<(bool Success, string Message)> SoftDeleteContentAsync(int id);

    Task<(bool Success, string Message)> RestoreContentAsync(int id);
}
