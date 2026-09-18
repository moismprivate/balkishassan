using System.Net;
using System.Text.RegularExpressions;

namespace BalkisHassan.Web.Utilities;

public static partial class SeoText
{
    public static string Description(string? html, string fallback, int maximumLength = 160)
    {
        var value = Whitespace().Replace(WebUtility.HtmlDecode(Tags().Replace(html ?? string.Empty, " ")), " ").Trim();
        if (string.IsNullOrWhiteSpace(value)) value = fallback;
        return value.Length <= maximumLength ? value : value[..maximumLength].TrimEnd() + "…";
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
