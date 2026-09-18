using System.ComponentModel.DataAnnotations;

namespace BalkisHassan.Domain;

public sealed class ContactInfo
{
    public int Id { get; set; }

    [Required, MaxLength(300)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(320), EmailAddress]
    public string? Email { get; set; }

    [MaxLength(100)]
    public string? Telephone { get; set; }

    [MaxLength(100)]
    public string? Mobile { get; set; }

    [MaxLength(1_000)]
    public string? Address { get; set; }

    public string? AdditionalInfo { get; set; }

    public bool IsVisible { get; set; } = true;

    public int? LegacyJoomlaId { get; set; }
}
