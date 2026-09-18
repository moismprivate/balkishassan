using Ganss.Xss;

namespace BalkisHassan.Web.Services;

public sealed class ContentSanitizer
{
    private readonly HtmlSanitizer _sanitizer = new();

    public ContentSanitizer()
    {
        _sanitizer.AllowedTags.UnionWith([
            "p", "br", "strong", "b", "em", "i", "u", "blockquote", "ul", "ol", "li",
            "h2", "h3", "h4", "a", "img", "figure", "figcaption", "audio", "source", "span", "div"
        ]);
        _sanitizer.AllowedAttributes.UnionWith([
            "href", "src", "alt", "title", "controls", "preload", "class", "dir", "lang", "width", "height"
        ]);
        _sanitizer.AllowedSchemes.UnionWith(["https", "http", "mailto"]);
        _sanitizer.AllowDataAttributes = false;
    }

    public string Sanitize(string? html) => _sanitizer.Sanitize(html ?? string.Empty);
}
