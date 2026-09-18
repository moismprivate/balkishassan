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
    public async Task AudioPagina_BiedtSpelerFallbackEnByteRangeStreaming()
    {
        using var client = _factory.CreateClient();
        var html = await client.GetStringAsync("/content/audio-4701-قيود-صارمة-على-المرأة");
        Assert.Contains("data-audio-player", html);
        Assert.Contains("type=\"audio/mpeg\"", html);
        Assert.Contains("تنزيل ملف MP3", html);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/uploads/audio/2291005-df9bd8b5.mp3");
        request.Headers.Range = new RangeHeaderValue(0, 1023);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        Assert.Equal("audio/mpeg", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(1024, response.Content.Headers.ContentLength);
    }
}
