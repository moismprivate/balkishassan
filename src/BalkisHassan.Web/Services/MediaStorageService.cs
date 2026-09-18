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
        await using var input = file.OpenReadStream();
        return await SaveAsync(input, file.FileName, file.Length, altText, cancellationToken);
    }

    public async Task<MediaItem> SaveAsync(Stream input, string originalFileName, long length, string? altText,
        CancellationToken cancellationToken)
    {
        if (length is <= 0 or > MaximumSize)
            throw new InvalidOperationException("حجم الملف غير مسموح به. الحد الأقصى 25 ميغابايت.");

        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
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
        await input.CopyToAsync(stream, cancellationToken);

        return new MediaItem
        {
            FileName = safeName, OriginalFileName = Path.GetFileName(originalFileName), Path = relativePath,
            MimeType = mimeType, Size = length, AltText = string.IsNullOrWhiteSpace(altText) ? null : altText.Trim()
        };
    }
}
