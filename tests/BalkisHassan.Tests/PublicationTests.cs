using BalkisHassan.Domain;

namespace BalkisHassan.Tests;

public sealed class PublicationTests
{
    [Fact]
    public void PublicatieFilter_VerbergtConceptenEnToekomstigePublicaties_EnSorteertNieuwsteEerst()
    {
        var now = DateTimeOffset.UtcNow;
        var items = new[]
        {
            new ContentItem { Title = "قديم", IsPublished = true, PublishedAt = now.AddDays(-3) },
            new ContentItem { Title = "جديد", IsPublished = true, PublishedAt = now.AddDays(-1) },
            new ContentItem { Title = "مستقبل", IsPublished = true, PublishedAt = now.AddDays(1) },
            new ContentItem { Title = "مسودة", IsPublished = false, PublishedAt = now.AddDays(-2) }
        };

        var result = items.Where(x => x.IsPublished && (x.PublishedAt == null || x.PublishedAt <= now))
            .OrderByDescending(x => x.PublishedAt.HasValue).ThenByDescending(x => x.PublishedAt).ToList();

        Assert.Equal(["جديد", "قديم"], result.Select(x => x.Title));
    }
}
