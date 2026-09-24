using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.RegularExpressions;
using BalkisHassan.Domain;
using BalkisHassan.Web.Utilities;

namespace BalkisHassan.Web.Components.Pages.Admin;

public sealed class AdminContentFormModel : IValidatableObject
{
    public string Title { get; set; } = string.Empty;

    public string? Slug { get; set; }

    public string? Summary { get; set; }

    public string Content { get; set; } = string.Empty;
    public string? YouTubeUrl { get; set; }

    public int CategoryId { get; set; }

    public ContentType Type { get; set; } = ContentType.Article;
    public DateTime? PublishedAt { get; set; } = DateTime.Now;
    public bool IsPublished { get; set; }
    public bool Featured { get; set; }
    public int SortOrder { get; set; }
    public string? MainImage { get; set; }
    public string? AudioPath { get; set; }
    public string? DocumentPath { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Title))
            yield return new ValidationResult("اكتبي عنوان المحتوى.", [nameof(Title)]);
        else if (Title.Length > 300)
            yield return new ValidationResult("العنوان طويل جداً (الحد الأقصى 300 حرف).", [nameof(Title)]);

        if (Slug?.Length > 320)
            yield return new ValidationResult("الرابط المختصر طويل جداً.", [nameof(Slug)]);

        if (Summary?.Length > 2000)
            yield return new ValidationResult("المقدمة القصيرة طويلة جداً.", [nameof(Summary)]);

        if (CategoryId < 1)
            yield return new ValidationResult("اختاري التصنيف الذي سيظهر فيه المحتوى.", [nameof(CategoryId)]);

        if (!string.IsNullOrWhiteSpace(YouTubeUrl) && YouTubeEmbed.ParseVideoIdFromUrl(YouTubeUrl) is null)
        {
            yield return new ValidationResult("ألصقي رابط فيديو صحيحاً من YouTube أو youtu.be.", [nameof(YouTubeUrl)]);
        }

        var readableText = WebUtility.HtmlDecode(Regex.Replace(Content ?? string.Empty, "<[^>]*>", string.Empty));
        if (string.IsNullOrWhiteSpace(readableText) &&
            string.IsNullOrWhiteSpace(YouTubeUrl) &&
            string.IsNullOrWhiteSpace(AudioPath))
        {
            yield return new ValidationResult("أضيفي نصاً أو رابط يوتيوب أو تسجيلاً صوتياً.", [nameof(Content)]);
        }
    }
}
