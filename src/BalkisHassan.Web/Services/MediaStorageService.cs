using System.Security.Cryptography;
using BalkisHassan.Domain;

namespace BalkisHassan.Web.Services;

public sealed class MediaStorageService(IWebHostEnvironment environment)
{
    private const long MaximumSize = 25 * 1024 * 1024;
    private static readonly IReadOnlyDictionary<string, string> AllowedTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".png"] = "image/png",
        [".webp"] = "image/webp", [".gif"] = "image/gif", [".mp3"] = "audio/mpeg",
        [".ogg"] = "audio/ogg", [".wav"] = "audio/wav", [".pdf"] = "application/pdf"
    };

    public async Task<MediaItem> SaveAsync(IFormFile file, string? altText, CancellationToken cancellationToken)
    {
        if (file.Length is <= 0 or > MaximumSize)
            throw new InvalidOperationException("حجم الملف غير مسموح به. الحد الأقصى 25 ميغابايت.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedTypes.TryGetValue(extension, out var mimeType))
            throw new InvalidOperationException("نوع الملف غير مسموح به.");

        var category = mimeType.StartsWith("image/", StringComparison.Ordinal) ? "images"
            : mimeType.StartsWith("audio/", StringComparison.Ordinal) ? "audio" : "documents";
        var safeName = $"{DateTime.UtcNow:yyyyMMdd}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(10)).ToLowerInvariant()}{extension}";
        var relativePath = $"/uploads/{category}/{safeName}";
        var directory = Path.Combine(environment.WebRootPath, "uploads", category);
        Directory.CreateDirectory(directory);
        var fullPath = Path.Combine(directory, safeName);

        await using var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
        await file.CopyToAsync(stream, cancellationToken);

        return new MediaItem
        {
            FileName = safeName, OriginalFileName = Path.GetFileName(file.FileName), Path = relativePath,
            MimeType = mimeType, Size = file.Length, AltText = string.IsNullOrWhiteSpace(altText) ? null : altText.Trim()
        };
    }
}
