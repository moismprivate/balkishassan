using BalkisHassan.Domain;

namespace BalkisHassan.Tests;

public sealed class SlugGeneratorTests
{
    [Theory]
    [InlineData("قصيدة جديدة", "قصيدة-جديدة")]
    [InlineData("  كتب   بلقيس  ", "كتب-بلقيس")]
    [InlineData("Hello, World!", "hello-world")]
    public void Generate_MaaktStabieleUnicodeSlug(string value, string expected)
    {
        Assert.Equal(expected, SlugGenerator.Generate(value));
    }
}
