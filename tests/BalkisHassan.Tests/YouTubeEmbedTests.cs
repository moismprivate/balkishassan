extern alias WebProject;

using YouTubeEmbed = WebProject::BalkisHassan.Web.Utilities.YouTubeEmbed;

namespace BalkisHassan.Tests;

public sealed class YouTubeEmbedTests
{
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=pvKosp0S4tc", "pvKosp0S4tc")]
    [InlineData("https://youtu.be/pvKosp0S4tc", "pvKosp0S4tc")]
    [InlineData("https://www.youtube.com/embed/pvKosp0S4tc", "pvKosp0S4tc")]
    public void FindVideoId_HerkentGebruikelijkeYoutubeLinks(string url, string expected)
    {
        Assert.Equal(expected, YouTubeEmbed.FindVideoId(url));
    }

    [Fact]
    public void AppendVideoLink_VervangtOudeLinkEnVoegtGeenDubbeleVideoToe()
    {
        const string original = "<p>النص</p><p><a href=\"https://youtu.be/HdjgYpC0OOA\">قديم</a></p>";

        var result = YouTubeEmbed.AppendVideoLink(original, "pvKosp0S4tc");

        Assert.Contains("<p>النص</p>", result);
        Assert.DoesNotContain("HdjgYpC0OOA", result);
        Assert.Equal(1, result.Split("youtube.com/watch", StringSplitOptions.None).Length - 1);
        Assert.Contains("https://www.youtube.com/watch?v=pvKosp0S4tc", result);
    }
}
