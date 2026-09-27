using JamesThew.ViewModels;
using Microsoft.AspNetCore.Http;

namespace JamesThew.Services;

public interface IMediaService
{
    Task<MediaLibraryViewModel> GetMediaLibraryAsync(string? searchQuery = null);
    Task<(bool Success, string Message, string? Url, string? FileName)> UploadImageAsync(IFormFile? file);
    Task<(bool Success, string Message, int UnlinkedCount)> DeleteMediaAsync(string fileName);
    Task<IReadOnlyList<MediaItemDto>> GetPickerMediaAsync(string? searchQuery = null);
    string GetPhysicalUploadPath();
}
