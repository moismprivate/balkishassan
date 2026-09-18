using System.Text;
using BalkisHassan.JoomlaMigration;
using Microsoft.Extensions.Configuration;

Console.OutputEncoding = Encoding.UTF8;
var configuration = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>(optional: true)
    .Build();
var connectionString = configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("De PostgreSQL-connection-string 'DefaultConnection' ontbreekt in User Secrets of de omgeving.");

var solutionRoot = FindSolutionRoot();
var sourceRoot = GetArgument("--source-root") ?? @"C:\_tjip\Mohannad\website\public_html";
var sqlPath = GetArgument("--sql") ?? @"C:\_tjip\Mohannad\website\balkisha-2.sql";
var webRoot = GetArgument("--web-root") ?? Path.Combine(solutionRoot, "src", "BalkisHassan.Web", "wwwroot");

Console.WriteLine($"Bron: {sourceRoot}");
Console.WriteLine($"Doel voor media: {webRoot}");
var report = await new MigrationRunner(connectionString, sqlPath, sourceRoot, webRoot).RunAsync();

var reportPath = Path.Combine(solutionRoot, "migration-report.md");
var analysisPath = Path.Combine(solutionRoot, "migration-analysis.md");
await File.WriteAllTextAsync(reportPath, BuildReport(report), new UTF8Encoding(false));
await File.WriteAllTextAsync(analysisPath, BuildAnalysis(report, sqlPath, sourceRoot), new UTF8Encoding(false));

Console.WriteLine();
Console.WriteLine("Migratie voltooid:");
Console.WriteLine($"- categorieën: {report.Categories}");
Console.WriteLine($"- inhoud: {report.ContentItems}");
Console.WriteLine($"- externe links: {report.ExternalLinks}");
Console.WriteLine($"- contacten: {report.Contacts}");
Console.WriteLine($"- media: {report.Media}");
Console.WriteLine($"- redirects: {report.Redirects}");
Console.WriteLine($"Rapport: {reportPath}");

string? GetArgument(string name)
{
    var index = Array.FindIndex(args, x => x.Equals(name, StringComparison.OrdinalIgnoreCase));
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

string FindSolutionRoot()
{
    var current = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "BalkisHassan.sln"))) return current.FullName;
        current = current.Parent;
    }
    throw new DirectoryNotFoundException("BalkisHassan.sln is niet gevonden. Start de migratietool vanuit de solutionmap.");
}

static string BuildReport(MigrationReport report)
{
    var warnings = report.Warnings.Count == 0 ? "- Geen." : string.Join('\n', report.Warnings.Select(x => $"- {x}"));
    return $$"""
        # Joomla-migratierapport

        Gegenereerd: {{DateTimeOffset.Now:yyyy-MM-dd HH:mm zzz}}

        ## Resultaat

        | Onderdeel | Aantal |
        |---|---:|
        | Categorieën | {{report.Categories}} |
        | Contentitems | {{report.ContentItems}} |
        | Externe links | {{report.ExternalLinks}} |
        | Contactrecords | {{report.Contacts}} |
        | Mediabestanden | {{report.Media}} |
        | Legacy-redirects | {{report.Redirects}} |

        ## Gemigreerde categorieën

        {{string.Join('\n', report.CategoryNames.Select(x => $"- {x}"))}}

        ## Encodingcontrole

        - Reeksen van vier of meer vraagtekens: {{report.QuestionRuns}}
        - Unicode replacement characters in gemigreerde inhoud: {{report.ReplacementCharacters}}

        ## Waarschuwingen

        {{warnings}}
        """;
}

static string BuildAnalysis(MigrationReport report, string sqlPath, string sourceRoot)
{
    var sourceRows = string.Join('\n', report.SourceRows.OrderBy(x => x.Key).Select(x => $"| `{x.Key}` | {x.Value} |"));
    var files = Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories).ToList();
    return $$"""
        # Migratieanalyse oude Joomla-website

        ## Bronnen

        - SQL-dump: `{{sqlPath}}`
        - Joomla-documentroot: `{{sourceRoot}}`
        - Joomla-versie: 1.5.14 (vastgesteld uit `libraries/joomla/version.php`)
        - Publieke bestanden aangetroffen: {{files.Count}}

        ## Database en encoding

        De dump is een phpMyAdmin/MySQL-export met tabellen onder de prefix `jos_`. De tabellen gebruiken hoofdzakelijk
        `utf8mb3_general_ci`. Het dumpbestand is zonder BOM opgeslagen en is als strikte UTF-8 valide. De importer leest
        het bestand daarom rechtstreeks als UTF-8 en voert geen extra tekenconversie uit.

        | Brontabel | Gelezen rijen |
        |---|---:|
        {{sourceRows}}

        ## Migratiestrategie

        De migratietool leest alleen `INSERT`-statements van relevante tabellen, decodeert MySQL-escapes, saneert oude HTML,
        behoudt tekst en regeleinden, herschrijft lokale media-URL's en schrijft idempotent naar PostgreSQL op basis van
        `LegacyJoomlaId`. Oude gebruikers en wachtwoordhashes worden bewust niet gemigreerd.

        Media worden uitsluitend gekopieerd; de oude Joomla-directory blijft ongewijzigd. Bestandsnamen krijgen een veilige,
        stabiele hash om botsingen te voorkomen. Scripts, events en onveilige embeds uit oude HTML worden verwijderd.

        ## Opvallende bevindingen

        - De actieve documentroot is `public_html`; `httpdocs` is leeg.
        - Drie lokale MP3-bestanden zijn gekoppeld vanuit de oude `mod_vplayer`-module.
        - PDF's en boekcovers staan hoofdzakelijk direct onder `images` en `images/stories`.
        - Hoofdmenu- en artikel-URL's krijgen permanente redirects.
        - Het opnieuw uitvoeren van de importer actualiseert bestaande records en maakt geen duplicaten.
        """;
}

public partial class Program;
