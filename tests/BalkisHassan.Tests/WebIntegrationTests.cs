extern alias WebProject;

using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using WebProgram = WebProject::Program;

namespace BalkisHassan.Tests;

public sealed class WebIntegrationTests : IClassFixture<WebApplicationFactory<WebProgram>>
{
    private readonly WebApplicationFactory<WebProgram> _factory;

    public WebIntegrationTests(WebApplicationFactory<WebProgram> factory) => _factory = factory;

    [Fact]
    public async Task Admin_VereistAuthenticatie()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/admin");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/admin/login", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task OudeJoomlaMenuUrl_GeeftPermanenteRedirect()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/index.php?option=com_content&view=category&id=9&Itemid=5");
        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        Assert.Equal("/category/قصائد", Uri.UnescapeDataString(location));
        Assert.All(location, character => Assert.True(character <= 127));
    }

    [Fact]
    public async Task Homepage_BevatArabischeUtf8Content()
    {
        using var client = _factory.CreateClient();
        var html = await client.GetStringAsync("/");
        Assert.Contains("بلقيس حميد حسن", html);
        Assert.DoesNotContain("????", html);
        Assert.Contains("dir=\"rtl\"", html);
    }

    [Fact]
    public async Task LoginFormulier_AccepteertCsrfTokenEnToontVeiligeFoutmelding()
    {
        using var client = _factory.CreateClient();
        var html = await client.GetStringAsync("/admin/login");
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success);

        var response = await client.PostAsync("/admin/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = "niet-bestaand@example.invalid",
            ["Password"] = "GeenEchtWachtwoord!123",
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(match.Groups[1].Value)
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var responseHtml = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("البريد الإلكتروني أو كلمة المرور غير صحيحة", responseHtml);
    }

    [Fact]
    public async Task HealthCheck_ControleertApplicatieEnPostgreSql()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OnbekendeRoute_ToontArabische404PaginaMetCorrecteStatus()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/deze-pagina-bestaat-niet");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("الصفحة غير موجودة", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Overzicht_GebruiktGeenGroteLetterplaceholdersVoorOntbrekendeAfbeeldingen()
    {
        using var client = _factory.CreateClient();
        var html = await client.GetStringAsync("/category/من-مقالات-الشاعرة");
        Assert.DoesNotContain("card-image\"><span", html);
        Assert.Contains("content-card without-image", html);
    }

    [Fact]
    public async Task Homepage_ToontPortretEnOorspronkelijkeLevensverhaal()
    {
        using var client = _factory.CreateClient();
        var html = await client.GetStringAsync("/");
        Assert.Contains("class=\"brand-photo\"", html);
        Assert.Contains("من هي بلقيس حميد حسن؟", html);
        Assert.Contains("اقرأ السيرة كاملة", html);
        Assert.Contains("/content/2009-09-13-11-47-08", html);
    }

    [Fact]
    public async Task AudioPagina_BiedtSpelerFallbackEnByteRangeStreaming()
    {
        using var client = _factory.CreateClient();
        var html = await client.GetStringAsync("/content/audio-4701-قيود-صارمة-على-المرأة");
        Assert.Contains("data-audio-player", html);
        Assert.Contains("type=\"audio/mpeg\"", html);
        Assert.Contains("تشغيل الصوت", html);
        Assert.Contains("تنزيل ملف MP3", html);
        Assert.DoesNotContain("autoplay", html, StringComparison.OrdinalIgnoreCase);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/uploads/audio/2291005-df9bd8b5.mp3");
        request.Headers.Range = new RangeHeaderValue(0, 1023);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        Assert.Equal("audio/mpeg", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(1024, response.Content.Headers.ContentLength);
    }

    [Fact]
    public async Task YouTubePagina_ToontPrivacyvriendelijkeEmbedZonderAutoplay()
    {
        using var client = _factory.CreateClient();
        var html = await client.GetStringAsync("/content/2019-08-15-09-31-22");
        Assert.Contains("class=\"video-embed\"", html);
        Assert.Contains("https://www.youtube-nocookie.com/embed/pvKosp0S4tc", html);
        Assert.DoesNotContain("autoplay=1", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OudeFlashVideos_ZijnHersteldAlsModerneYouTubeEmbeds()
    {
        using var client = _factory.CreateClient();
        var html = await client.GetStringAsync("/content/2011-02-13-17-00-04");
        Assert.Contains("https://www.youtube-nocookie.com/embed/HdjgYpC0OOA", html);
        Assert.DoesNotContain("shockwave-flash", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("autoplay=1", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Boekpagina_BiedtPdfEnBiografieVergrootPortretNiet()
    {
        using var client = _factory.CreateClient();
        var book = await client.GetStringAsync("/content/2009-09-13-10-56-06");
        Assert.Contains("href=\"/uploads/documents/aghtirab-altair.pdf\"", book);
        Assert.Contains("تنزيل الكتاب بصيغة PDF", book);
        using var pdf = await client.GetAsync("/uploads/documents/aghtirab-altair.pdf");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);

        var biography = await client.GetStringAsync("/content/2009-09-13-11-47-08");
        Assert.Contains("article-page biography-page", biography);
        Assert.Contains("width=\"138\" height=\"166\"", biography);
    }

    [Fact]
    public async Task JoomlaUrlMetContentPrefix_RedirectNaarNieuweBoekpagina()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/content/index.php?option=com_content&view=article&id=1");
        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.Equal("/content/2009-09-13-10-56-06", response.Headers.Location?.OriginalString);

        var originalUrl = await client.GetAsync("/index.php?option=com_content&view=article&id=1&Itemid=2");
        Assert.Equal(HttpStatusCode.MovedPermanently, originalUrl.StatusCode);
        Assert.Equal("/content/2009-09-13-10-56-06", originalUrl.Headers.Location?.OriginalString);
    }
}
