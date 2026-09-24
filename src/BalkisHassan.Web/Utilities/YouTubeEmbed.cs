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

    public static string CanonicalUrl(string videoId) => $"https://www.youtube.com/watch?v={videoId}";

    public static string? ParseVideoIdFromUrl(string? value)
    {
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("https" or "http"))
            return null;

        var host = uri.Host.ToLowerInvariant();
        if (host is not ("youtube.com" or "www.youtube.com" or "m.youtube.com" or
            "youtube-nocookie.com" or "www.youtube-nocookie.com" or "youtu.be" or "www.youtu.be"))
            return null;

        var id = FindVideoId(uri.AbsoluteUri);
        return id is { Length: 11 } ? id : null;
    }

    public static string AppendVideoLink(string html, string videoId)
    {
        var content = RemoveVideoLinks(html);
        var separator = string.IsNullOrWhiteSpace(content) ? string.Empty : Environment.NewLine;
        return $"{content}{separator}<p><a href=\"{CanonicalUrl(videoId)}\">مشاهدة الفيديو على يوتيوب</a></p>";
    }

    [GeneratedRegex(@"(?:youtu\.be/|youtube(?:-nocookie)?\.com/(?:watch\?(?:[^\s""'<>]*&)?v=|embed/|v/|shorts/|live/))([A-Za-z0-9_-]{11})(?![A-Za-z0-9_-])", RegexOptions.IgnoreCase)]
    private static partial Regex YouTubeUrl();

    [GeneratedRegex(@"<a\b[^>]*href=[""'][^""']*(?:youtu\.be|youtube(?:-nocookie)?\.com)[^""']*[""'][^>]*>.*?</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex YouTubeAnchor();

    [GeneratedRegex(@"https?://(?:www\.)?(?:youtu\.be|youtube(?:-nocookie)?\.com)/[^\s<]+", RegexOptions.IgnoreCase)]
    private static partial Regex YouTubePlainUrl();
}
