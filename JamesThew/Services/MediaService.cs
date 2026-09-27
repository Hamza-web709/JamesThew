using System.Text.RegularExpressions;
using JamesThew.Data;
using JamesThew.ViewModels;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace JamesThew.Services;

public class MediaService(ApplicationDbContext db, IWebHostEnvironment env) : IMediaService
{
    public const long MaxFileSize = 5 * 1024 * 1024; // 5 MB
    public const string UploadsRelativePath = "/uploads/editorial";

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/pjpeg",
        "image/png",
        "image/webp"
    };

    public string GetPhysicalUploadPath()
    {
        var webRoot = env.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
        {
            webRoot = Path.Combine(env.ContentRootPath, "wwwroot");
        }

        var uploadDir = Path.Combine(webRoot, "uploads", "editorial");
        if (!Directory.Exists(uploadDir))
        {
            Directory.CreateDirectory(uploadDir);
        }

        return uploadDir;
    }

    public async Task<MediaLibraryViewModel> GetMediaLibraryAsync(string? searchQuery = null)
    {
        var uploadDir = GetPhysicalUploadPath();
        var dirInfo = new DirectoryInfo(uploadDir);

        var fileInfos = dirInfo.Exists
            ? dirInfo.GetFiles("*.*", SearchOption.TopDirectoryOnly)
                .Where(f => AllowedExtensions.Contains(f.Extension))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .ToList()
            : [];

        // Query all content items that reference editorial uploads
        var referencedItems = await db.ContentItems
            .AsNoTracking()
            .Where(x => x.ImageUrl != null && x.ImageUrl.Contains(UploadsRelativePath))
            .Select(x => new
            {
                x.Id,
                x.Title,
                x.Kind,
                x.Slug,
                x.ImageUrl
            })
            .ToListAsync();

        var items = new List<MediaItemDto>();
        long totalBytes = 0;

        foreach (var file in fileInfos)
        {
            totalBytes += file.Length;
            var webUrl = $"{UploadsRelativePath}/{file.Name}";

            var usages = referencedItems
                .Where(r => r.ImageUrl != null && (string.Equals(r.ImageUrl, webUrl, StringComparison.OrdinalIgnoreCase) || r.ImageUrl.EndsWith("/" + file.Name, StringComparison.OrdinalIgnoreCase)))
                .Select(r => new MediaItemUsageDto
                {
                    ContentId = r.Id,
                    Title = r.Title,
                    Kind = r.Kind,
                    Slug = r.Slug
                })
                .ToList();

            var dto = new MediaItemDto
            {
                FileName = file.Name,
                Url = webUrl,
                SizeBytes = file.Length,
                FormattedSize = FormatBytes(file.Length),
                CreatedAtUtc = file.CreationTimeUtc,
                UsageCount = usages.Count,
                Usages = usages
            };

            if (string.IsNullOrWhiteSpace(searchQuery) ||
                dto.FileName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
                dto.Usages.Any(u => u.Title.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)))
            {
                items.Add(dto);
            }
        }

        return new MediaLibraryViewModel
        {
            Items = items,
            TotalCount = items.Count,
            TotalSizeBytes = totalBytes,
            FormattedTotalSize = FormatBytes(totalBytes),
            SearchQuery = searchQuery
        };
    }

    public async Task<IReadOnlyList<MediaItemDto>> GetPickerMediaAsync(string? searchQuery = null)
    {
        var model = await GetMediaLibraryAsync(searchQuery);
        return model.Items;
    }

    public async Task<(bool Success, string Message, string? Url, string? FileName)> UploadImageAsync(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            return (false, "No file was uploaded or the uploaded file is empty.", null, null);
        }

        // 1. Max size validation (5 MB)
        if (file.Length > MaxFileSize)
        {
            return (false, $"File size exceeds the 5 MB limit ({FormatBytes(file.Length)}).", null, null);
        }

        var rawFileName = file.FileName ?? string.Empty;

        // 2. Path traversal detection in file name
        if (rawFileName.Contains('/') || rawFileName.Contains('\\') || rawFileName.Contains(".."))
        {
            return (false, "Security violation: Path traversal characters are not permitted in file name.", null, null);
        }

        // 3. Extension validation
        var ext = Path.GetExtension(rawFileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
        {
            return (false, $"Unsupported file format '{ext}'. Allowed formats are: .jpg, .jpeg, .png, .webp.", null, null);
        }

        // 4. MIME type validation
        var mime = file.ContentType?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!AllowedMimeTypes.Contains(mime))
        {
            return (false, $"Unsupported MIME type '{mime}'. Allowed image MIME types: image/jpeg, image/png, image/webp.", null, null);
        }

        // 5. Magic bytes inspection
        var header = new byte[16];
        using (var stream = file.OpenReadStream())
        {
            var read = await stream.ReadAsync(header.AsMemory(0, 16));
            if (read < 12)
            {
                return (false, "The file header is incomplete or corrupt.", null, null);
            }
        }

        var (isValidMagic, detectedType) = ValidateMagicBytes(header);
        if (!isValidMagic)
        {
            return (false, "File signature verification failed: the file content does not match a valid image.", null, null);
        }

        // Verify detected magic type matches extension
        if ((detectedType == "jpeg" && ext != ".jpg" && ext != ".jpeg") ||
            (detectedType == "png" && ext != ".png") ||
            (detectedType == "webp" && ext != ".webp"))
        {
            return (false, $"File extension '{ext}' does not match its detected content type '{detectedType}'.", null, null);
        }

        // 6. Safe unique filename generation
        var baseName = Path.GetFileNameWithoutExtension(rawFileName);
        var sanitizedBase = Regex.Replace(baseName.ToLowerInvariant(), @"[^a-z0-9_-]", "-").Trim('-');
        if (string.IsNullOrWhiteSpace(sanitizedBase))
            sanitizedBase = "editorial";
        if (sanitizedBase.Length > 40)
            sanitizedBase = sanitizedBase[..40];

        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var uniqueFileName = $"{sanitizedBase}_{uniqueSuffix}{ext}";

        // 7. Path traversal validation on destination path
        var uploadDir = GetPhysicalUploadPath();
        var fullUploadDir = Path.GetFullPath(uploadDir);
        var targetPath = Path.Combine(uploadDir, uniqueFileName);
        var fullTarget = Path.GetFullPath(targetPath);

        if (!fullTarget.StartsWith(fullUploadDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return (false, "Security violation: Destination path falls outside the designated upload directory.", null, null);
        }

        // 8. Save file to disk
        using (var stream = file.OpenReadStream())
        using (var destStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write))
        {
            await stream.CopyToAsync(destStream);
        }

        var webUrl = $"{UploadsRelativePath}/{uniqueFileName}";
        return (true, "Image uploaded successfully.", webUrl, uniqueFileName);
    }

    public async Task<(bool Success, string Message, int UnlinkedCount)> DeleteMediaAsync(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return (false, "File name cannot be empty.", 0);
        }

        // Prevent path traversal
        if (fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains(".."))
        {
            return (false, "Security violation: Path traversal characters are not permitted.", 0);
        }

        var cleanName = Path.GetFileName(fileName);
        var uploadDir = GetPhysicalUploadPath();
        var fullUploadDir = Path.GetFullPath(uploadDir);
        var targetPath = Path.Combine(uploadDir, cleanName);
        var fullTarget = Path.GetFullPath(targetPath);

        if (!fullTarget.StartsWith(fullUploadDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return (false, "Security violation: Target path is outside the designated upload directory.", 0);
        }

        if (!File.Exists(targetPath))
        {
            return (false, $"Media file '{cleanName}' was not found on the server.", 0);
        }

        // Safe deletion policy:
        // Automatically unlink any ContentItems referencing this image URL so public views omit the image cleanly.
        var webUrl = $"{UploadsRelativePath}/{cleanName}";
        var referencingItems = await db.ContentItems
            .Where(x => x.ImageUrl != null && (x.ImageUrl == webUrl || x.ImageUrl.EndsWith("/" + cleanName)))
            .ToListAsync();

        var unlinkedCount = referencingItems.Count;
        foreach (var item in referencingItems)
        {
            item.ImageUrl = null;
            item.UpdatedAtUtc = DateTime.UtcNow;
        }

        if (unlinkedCount > 0)
        {
            await db.SaveChangesAsync();
        }

        try
        {
            File.Delete(targetPath);
        }
        catch (Exception ex)
        {
            return (false, $"Failed to delete physical file: {ex.Message}", unlinkedCount);
        }

        var message = unlinkedCount > 0
            ? $"File '{cleanName}' safely removed. Unlinked from {unlinkedCount} content item(s)."
            : $"File '{cleanName}' safely removed.";

        return (true, message, unlinkedCount);
    }

    private static (bool IsValid, string DetectedType) ValidateMagicBytes(byte[] header)
    {
        // JPEG: FF D8 FF
        if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return (true, "jpeg");
        }

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
            header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
        {
            return (true, "png");
        }

        // WEBP: 'RIFF' (offset 0..3) ... 'WEBP' (offset 8..11)
        if (header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
            header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
        {
            return (true, "webp");
        }

        return (false, "unknown");
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F2} MB";
    }
}
