using System.Text;
using BalkisHassan.JoomlaMigration;

namespace BalkisHassan.Tests;

public sealed class JoomlaSqlParserTests
{
    [Fact]
    public void Parse_BehoudtArabischEnMySqlRegeleinden()
    {
        var path = Path.Combine(Path.GetTempPath(), $"joomla-{Guid.NewGuid():N}.sql");
        try
        {
            File.WriteAllText(path,
                "INSERT INTO `jos_content` (`id`, `title`, `introtext`) VALUES\n(1, 'قصيدة', 'سطر أول\\r\\nسطر ثان');",
                new UTF8Encoding(false));
            var result = new JoomlaSqlParser().Parse(path, "jos_content")["jos_content"].Single();
            Assert.Equal("قصيدة", result.Text("title"));
            Assert.Equal("سطر أول\r\nسطر ثان", result.Text("introtext"));
            Assert.DoesNotContain("????", result.Text("introtext"));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
