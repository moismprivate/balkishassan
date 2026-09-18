using System.ComponentModel.DataAnnotations;

namespace BalkisHassan.Domain;

public sealed class ContentItem
{
    public int Id { get; set; }

    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(320)]
    public string Slug { get; set; } = string.Empty;

    [MaxLength(2_000)]
    public string? Summary { get; set; }

    [Required]
    public string Content { get; set; } = string.Empty;

    public ContentType Type { get; set; } = ContentType.Article;

    public int CategoryId { get; set; }

    public Category Category { get; set; } = null!;

    public DateTimeOffset? PublishedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public bool IsPublished { get; set; }

    public bool Featured { get; set; }

    public int SortOrder { get; set; }

    [MaxLength(500)]
    public string? MainImage { get; set; }

    [MaxLength(500)]
    public string? AudioPath { get; set; }

    [MaxLength(500)]
    public string? DocumentPath { get; set; }

    public int? LegacyJoomlaId { get; set; }

    [MaxLength(1_000)]
    public string? LegacyUrl { get; set; }
}
