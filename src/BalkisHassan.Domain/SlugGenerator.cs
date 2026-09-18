using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace BalkisHassan.Domain;

public static partial class SlugGenerator
{
    public static string Generate(string value, int? fallbackId = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallbackId is null ? "content" : $"content-{fallbackId}";
        }

        var normalized = value.Normalize(NormalizationForm.FormKC).Trim().ToLowerInvariant();
        normalized = UnsafeCharacters().Replace(normalized, "-");
        normalized = RepeatedDashes().Replace(normalized, "-").Trim('-');

        return string.IsNullOrWhiteSpace(normalized)
            ? fallbackId is null ? "content" : $"content-{fallbackId}"
            : normalized;
    }

    [GeneratedRegex(@"[^\p{L}\p{Nd}]+", RegexOptions.CultureInvariant)]
    private static partial Regex UnsafeCharacters();

    [GeneratedRegex("-{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex RepeatedDashes();
}
