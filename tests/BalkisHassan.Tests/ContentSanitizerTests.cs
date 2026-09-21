extern alias WebProject;

using ContentSanitizer = WebProject::BalkisHassan.Web.Services.ContentSanitizer;

namespace BalkisHassan.Tests;

public sealed class ContentSanitizerTests
{
    [Fact]
    public void Sanitize_BehoudtOndersteundeTekstopmaak()
    {
        var sanitizer = new ContentSanitizer();

        var result = sanitizer.Sanitize("<h2>عنوان</h2><p><strong>عريض</strong> و<em>مائل</em></p><blockquote>اقتباس</blockquote><ul><li>بند</li></ul>");

        Assert.Contains("<h2>عنوان</h2>", result);
        Assert.Contains("<strong>عريض</strong>", result);
        Assert.Contains("<em>مائل</em>", result);
        Assert.Contains("<blockquote>اقتباس</blockquote>", result);
        Assert.Contains("<ul><li>بند</li></ul>", result);
    }

    [Fact]
    public void Sanitize_VerwijdertUitvoerbareHtmlUitBronmodus()
    {
        var sanitizer = new ContentSanitizer();

        var result = sanitizer.Sanitize("<script>alert(1)</script><img src=\"/uploads/test.jpg\" onerror=\"alert(2)\"><a href=\"javascript:alert(3)\">link</a>");

        Assert.DoesNotContain("script", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onerror", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", result, StringComparison.OrdinalIgnoreCase);
    }
}
