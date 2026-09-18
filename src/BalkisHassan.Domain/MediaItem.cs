using System.ComponentModel.DataAnnotations;

namespace BalkisHassan.Domain;

public sealed class MediaItem
{
    public int Id { get; set; }

    [Required, MaxLength(260)]
    public string FileName { get; set; } = string.Empty;

    [Required, MaxLength(260)]
    public string OriginalFileName { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string Path { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string MimeType { get; set; } = string.Empty;

    public long Size { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [MaxLength(500)]
    public string? AltText { get; set; }

    [MaxLength(500)]
    public string? LegacyPath { get; set; }
}
