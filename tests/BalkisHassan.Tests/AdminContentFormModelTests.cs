extern alias WebProject;

using System.ComponentModel.DataAnnotations;
using AdminContentFormModel = WebProject::BalkisHassan.Web.Components.Pages.Admin.AdminContentFormModel;

namespace BalkisHassan.Tests;

public sealed class AdminContentFormModelTests
{
    [Fact]
    public void LeegFormulier_NoemtVerplichteVeldenInArabisch()
    {
        var errors = Validate(new AdminContentFormModel());

        Assert.Contains(errors, x => x.ErrorMessage == "اكتبي عنوان المحتوى.");
        Assert.Contains(errors, x => x.ErrorMessage == "اختاري التصنيف الذي سيظهر فيه المحتوى.");
        Assert.Contains(errors, x => x.ErrorMessage == "أضيفي نصاً أو رابط يوتيوب أو تسجيلاً صوتياً.");
    }

    [Fact]
    public void VideoZonderArtikeltekst_IsGeldigMetTitelEnCategorie()
    {
        var errors = Validate(new AdminContentFormModel
        {
            Title = "لقاء جديد",
            CategoryId = 1,
            YouTubeUrl = "https://www.youtube.com/shorts/pvKosp0S4tc"
        });

        Assert.Empty(errors);
    }

    [Fact]
    public void OngeldigeYouTubeLink_GeeftGerichteFout()
    {
        var errors = Validate(new AdminContentFormModel
        {
            Title = "لقاء جديد",
            CategoryId = 1,
            YouTubeUrl = "https://example.com/youtube.com/watch?v=pvKosp0S4tc"
        });

        Assert.Contains(errors, x => x.MemberNames.Contains(nameof(AdminContentFormModel.YouTubeUrl)));
    }

    [Fact]
    public void AudioZonderArtikeltekst_IsGeldigMetTitelEnCategorie()
    {
        var errors = Validate(new AdminContentFormModel
        {
            Title = "قراءة شعرية",
            CategoryId = 1,
            AudioPath = "/uploads/audio/test.mp3"
        });

        Assert.Empty(errors);
    }

    private static List<ValidationResult> Validate(AdminContentFormModel model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }
}
