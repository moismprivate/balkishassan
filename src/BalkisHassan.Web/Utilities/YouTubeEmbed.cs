using System.Net;
using System.Text.RegularExpressions;

namespace BalkisHassan.Web.Utilities;

public static partial class YouTubeEmbed
{
    public static string? FindVideoId(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;
        var match = YouTubeUrl().Match(WebUtility.HtmlDecode(html));
        return match.Success ? match.Groups[1].Value : null;
    }

    public static string RemoveVideoLinks(string html)
    {
        var withoutAnchors = YouTubeAnchor().Replace(html, string.Empty);
        return YouTubePlainUrl().Replace(withoutAnchors, string.Empty).Trim();
    }

    [GeneratedRegex(@"(?:youtu\.be/|youtube(?:-nocookie)?\.com/(?:watch\?(?:[^\s""'<>]*&)?v=|embed/|v/))([A-Za-z0-9_-]{11})", RegexOptions.IgnoreCase)]
    private static partial Regex YouTubeUrl();

    [GeneratedRegex(@"<a\b[^>]*href=[""'][^""']*(?:youtu\.be|youtube(?:-nocookie)?\.com)[^""']*[""'][^>]*>.*?</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex YouTubeAnchor();

    [GeneratedRegex(@"https?://(?:www\.)?(?:youtu\.be|youtube(?:-nocookie)?\.com)/[^\s<]+", RegexOptions.IgnoreCase)]
    private static partial Regex YouTubePlainUrl();
}
