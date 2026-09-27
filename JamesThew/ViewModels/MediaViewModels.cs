using System.ComponentModel.DataAnnotations;
using JamesThew.Models;
using Microsoft.AspNetCore.Http;

namespace JamesThew.ViewModels;

public class MediaItemDto
{
    public string FileName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string FormattedSize { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public int UsageCount { get; set; }
    public IReadOnlyList<MediaItemUsageDto> Usages { get; set; } = [];
}

public class MediaItemUsageDto
{
    public int ContentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public ContentKind Kind { get; set; }
    public string Slug { get; set; } = string.Empty;
}

public class MediaLibraryViewModel
{
    public IReadOnlyList<MediaItemDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public long TotalSizeBytes { get; set; }
    public string FormattedTotalSize { get; set; } = string.Empty;
    public string? SearchQuery { get; set; }
}

public class MediaUploadInputModel
{
    [Required(ErrorMessage = "Please select an image file to upload.")]
    public IFormFile? File { get; set; }
}
