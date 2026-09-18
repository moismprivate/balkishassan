using System.ComponentModel.DataAnnotations;

namespace BalkisHassan.Domain;

public sealed class LegacyRedirect
{
    public int Id { get; set; }

    [Required, MaxLength(1_000)]
    public string Source { get; set; } = string.Empty;

    [Required, MaxLength(1_000)]
    public string Destination { get; set; } = string.Empty;

    public bool IsPermanent { get; set; } = true;
}
