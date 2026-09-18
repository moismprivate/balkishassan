using System.ComponentModel.DataAnnotations;

namespace BalkisHassan.Domain;

public sealed class ExternalLink
{
    public int Id { get; set; }

    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(1_000), Url]
    public string Url { get; set; } = string.Empty;

    [MaxLength(2_000)]
    public string? Description { get; set; }

    public int SortOrder { get; set; }

    public bool IsVisible { get; set; } = true;

    public int? LegacyJoomlaId { get; set; }
}
