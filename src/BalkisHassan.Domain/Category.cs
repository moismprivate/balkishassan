using System.ComponentModel.DataAnnotations;

namespace BalkisHassan.Domain;

public sealed class Category
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(220)]
    public string Slug { get; set; } = string.Empty;

    [MaxLength(2_000)]
    public string? Description { get; set; }

    public int SortOrder { get; set; }

    public bool IsVisible { get; set; } = true;

    public int? LegacyJoomlaId { get; set; }

    public ICollection<ContentItem> ContentItems { get; set; } = [];
}
