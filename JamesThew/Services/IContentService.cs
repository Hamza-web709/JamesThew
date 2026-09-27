using JamesThew.Models;
using JamesThew.ViewModels;

namespace JamesThew.Services;

public interface IContentService
{
    Task<HomeViewModel> GetHomeDataAsync(bool canViewPaidContent);
    Task<FaqViewModel> GetFaqsAsync();
    Task<ContentSearchViewModel> SearchRecipesAsync(string? query, ContentVisibility? visibility, int page, int pageSize, bool canViewPaidContent);
    Task<ContentSearchViewModel> SearchTipsAsync(string? query, ContentVisibility? visibility, int page, int pageSize, bool canViewPaidContent);
    Task<ContentSearchViewModel> SearchAllAsync(string? query, ContentKind? kind, ContentVisibility? visibility, int page, int pageSize, bool canViewPaidContent);
    Task<RecipeDetailViewModel?> GetRecipeBySlugAsync(string slug, bool canViewPaidContent);
    Task<TipDetailViewModel?> GetTipBySlugAsync(string slug, bool canViewPaidContent);
}
