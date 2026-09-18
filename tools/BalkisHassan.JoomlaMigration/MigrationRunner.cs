using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using BalkisHassan.Domain;
using BalkisHassan.Infrastructure;
using Ganss.Xss;
using Microsoft.EntityFrameworkCore;

namespace BalkisHassan.JoomlaMigration;

public sealed class MigrationRunner(string connectionString, string sqlPath, string publicRoot, string webRoot)
{
    private static readonly string[] Tables =
    [
        "jos_categories", "jos_sections", "jos_content", "jos_content_frontpage", "jos_menu",
        "jos_modules", "jos_weblinks", "jos_contact_details"
    ];

    private static readonly IReadOnlyDictionary<int, (string Name, string Slug, ContentType Type, int Order)> MainCategories =
        new Dictionary<int, (string, string, ContentType, int)>
        {
            [0] = ("أرشيف غير مصنف", "أرشيف-غير-مصنف", ContentType.Article, 900),
            [17] = ("قصائد بصوت الشاعرة", "قصائد-بصوت-الشاعرة", ContentType.AudioPoem, 10),
            [6] = ("كتب بلقيس", "كتب-بلقيس", ContentType.Book, 20),
            [8] = ("من مقالات الشاعرة", "من-مقالات-الشاعرة", ContentType.Article, 30),
            [12] = ("محطات", "محطات", ContentType.Biography, 40),
            [7] = ("كتب عن الشاعرة", "كتب-عن-الشاعرة", ContentType.Book, 50),
            [9] = ("قصائد", "قصائد", ContentType.Poem, 60),
            [10] = ("لقاءات مع الشاعرة", "لقاءات-مع-الشاعرة", ContentType.Interview, 70)
        };

    private readonly HtmlSanitizer _sanitizer = CreateSanitizer();
    private readonly Dictionary<string, string> _mediaMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _warnings = [];

    public async Task<MigrationReport> RunAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sqlPath)) throw new FileNotFoundException("De Joomla SQL-dump ontbreekt.", sqlPath);
        if (!Directory.Exists(publicRoot)) throw new DirectoryNotFoundException($"De Joomla documentroot ontbreekt: {publicRoot}");

        Console.WriteLine("Joomla SQL-dump wordt als strikte UTF-8 gelezen...");
        var data = new JoomlaSqlParser().Parse(sqlPath, Tables);
        foreach (var table in Tables) Console.WriteLine($"{table}: {data[table].Count} rijen");

        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connectionString).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.MigrateAsync(cancellationToken);

        var mediaCount = await MigrateMediaAsync(db, data, cancellationToken);
        var categoryMap = await MigrateCategoriesAsync(db, data, cancellationToken);
        var contentCount = await MigrateContentAsync(db, data, categoryMap, cancellationToken);
        contentCount += await MigrateAudioModuleAsync(db, data, categoryMap, cancellationToken);
        var linkCount = await MigrateLinksAsync(db, data, cancellationToken);
        var contactCount = await MigrateContactsAsync(db, data, cancellationToken);
        var redirectCount = await MigrateRedirectsAsync(db, data, categoryMap, cancellationToken);

        var migratedText = string.Join('\n', await db.ContentItems.AsNoTracking().Select(x => x.Title + "\n" + x.Content).ToListAsync(cancellationToken));
        var questionRuns = Regex.Matches(migratedText, "\\?{4,}").Count;
        var replacements = migratedText.Count(x => x == '\uFFFD');
        if (questionRuns > 0) _warnings.Add($"Er zijn {questionRuns} reeksen met vier of meer vraagtekens in gemigreerde inhoud gevonden.");
        if (replacements > 0) _warnings.Add($"Er zijn {replacements} Unicode replacement characters in gemigreerde inhoud gevonden; deze stonden al in de bron.");

        var categoryNames = await db.Categories.AsNoTracking().OrderBy(x => x.SortOrder).Select(x => x.Name).ToListAsync(cancellationToken);
        return new MigrationReport(categoryMap.Count, contentCount, linkCount, contactCount, mediaCount, redirectCount,
            data.ToDictionary(x => x.Key, x => x.Value.Count), categoryNames, questionRuns, replacements, _warnings);
    }

    private async Task<Dictionary<int, Category>> MigrateCategoriesAsync(
        ApplicationDbContext db, IReadOnlyDictionary<string, List<JoomlaRow>> data, CancellationToken cancellationToken)
    {
        var contentCategoryIds = data["jos_content"].Select(x => x.Int("catid")).ToHashSet();
        contentCategoryIds.UnionWith(MainCategories.Keys);
        var sourceCategories = data["jos_categories"].Where(x => contentCategoryIds.Contains(x.Int("id"))).ToList();
        var existing = (await db.Categories.Where(x => x.LegacyJoomlaId != null).ToListAsync(cancellationToken))
            .ToDictionary(x => x.LegacyJoomlaId!.Value);
        var usedSlugs = (await db.Categories.Select(x => x.Slug).ToListAsync(cancellationToken)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var source in sourceCategories)
        {
            var legacyId = source.Int("id");
            if (!existing.TryGetValue(legacyId, out var category))
            {
                category = new Category { LegacyJoomlaId = legacyId };
                db.Categories.Add(category);
                existing[legacyId] = category;
            }

            var special = MainCategories.GetValueOrDefault(legacyId);
            var sourceName = FirstNonEmpty(source.Text("title"), source.Text("name"));
            category.Name = !string.IsNullOrWhiteSpace(special.Name) ? special.Name : sourceName;
            var wantedSlug = !string.IsNullOrWhiteSpace(special.Slug) ? special.Slug : SlugGenerator.Generate(category.Name, legacyId);
            if (category.Slug != wantedSlug && usedSlugs.Contains(wantedSlug)) wantedSlug += $"-{legacyId}";
            usedSlugs.Add(wantedSlug);
            category.Slug = wantedSlug;
            category.Description = Limit(PlainText(source.Text("description")), 2_000);
            category.SortOrder = special.Order > 0 ? special.Order : 100 + source.Int("ordering");
            category.IsVisible = source.Bool("published") || (MainCategories.ContainsKey(legacyId) && legacyId != 0);
        }

        foreach (var (legacyId, special) in MainCategories.Where(x => !existing.ContainsKey(x.Key)))
        {
            var category = new Category
            {
                LegacyJoomlaId = legacyId, Name = special.Name, Slug = special.Slug,
                SortOrder = special.Order, IsVisible = legacyId != 0
            };
            db.Categories.Add(category);
            existing[legacyId] = category;
        }

        await db.SaveChangesAsync(cancellationToken);
        return existing;
    }

    private async Task<int> MigrateContentAsync(ApplicationDbContext db,
        IReadOnlyDictionary<string, List<JoomlaRow>> data, IReadOnlyDictionary<int, Category> categories,
        CancellationToken cancellationToken)
    {
        var existing = (await db.ContentItems.Where(x => x.LegacyJoomlaId != null && x.LegacyJoomlaId > 0).ToListAsync(cancellationToken))
            .ToDictionary(x => x.LegacyJoomlaId!.Value);
        var usedSlugs = (await db.ContentItems.Select(x => x.Slug).ToListAsync(cancellationToken)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var featuredIds = data["jos_content_frontpage"].Select(x => x.Int("content_id")).ToHashSet();
        var migrated = 0;

        foreach (var source in data["jos_content"])
        {
            var legacyId = source.Int("id");
            var categoryId = source.Int("catid");
            if (!categories.TryGetValue(categoryId, out var category))
            {
                _warnings.Add($"Inhoud {legacyId} overgeslagen: categorie {categoryId} ontbreekt.");
                continue;
            }

            if (!existing.TryGetValue(legacyId, out var item))
            {
                item = new ContentItem { LegacyJoomlaId = legacyId };
                db.ContentItems.Add(item);
                existing[legacyId] = item;
            }

            item.Title = FirstNonEmpty(source.Text("title"), $"Joomla {legacyId}").Trim();
            var wantedSlug = SlugGenerator.Generate(FirstNonEmpty(source.Text("alias"), item.Title), legacyId);
            if (item.Slug != wantedSlug && usedSlugs.Contains(wantedSlug)) wantedSlug += $"-{legacyId}";
            usedSlugs.Add(wantedSlug);
            item.Slug = wantedSlug;
            var intro = RewriteMedia(source.Text("introtext"));
            var full = RewriteMedia(source.Text("fulltext"));
            var combined = string.IsNullOrWhiteSpace(full) ? intro : $"{intro}\n{full}";
            combined = RewriteLegacyYouTubeObjects(combined);
            item.Content = _sanitizer.Sanitize(combined);
            item.Summary = Limit(PlainText(intro), 800);
            item.CategoryId = category.Id;
            item.Type = MainCategories.TryGetValue(categoryId, out var special) ? special.Type : ContentType.Article;
            item.CreatedAt = ParseDate(source.Get("created")) ?? DateTimeOffset.UnixEpoch;
            item.UpdatedAt = ParseDate(source.Get("modified")) ?? item.CreatedAt;
            item.PublishedAt = ParseDate(source.Get("publish_up")) ?? ParseDate(source.Get("created"));
            item.IsPublished = source.Int("state") == 1;
            item.Featured = featuredIds.Contains(legacyId);
            item.SortOrder = source.Int("ordering");
            item.MainImage = FindAsset(item.Content, "img", "src", "image/");
            item.AudioPath = FindByExtension(item.Content, ".mp3", ".ogg", ".wav");
            item.DocumentPath = FindByExtension(item.Content, ".pdf");
            item.LegacyUrl = $"/index.php?option=com_content&view=article&id={legacyId}";
            migrated++;
        }

        await db.SaveChangesAsync(cancellationToken);
        return migrated;
    }

    private async Task<int> MigrateAudioModuleAsync(ApplicationDbContext db,
        IReadOnlyDictionary<string, List<JoomlaRow>> data, IReadOnlyDictionary<int, Category> categories,
        CancellationToken cancellationToken)
    {
        if (!categories.TryGetValue(17, out var category)) return 0;
        var count = 0;
        foreach (var module in data["jos_modules"].Where(x => x.Text("module").Equals("mod_vplayer", StringComparison.OrdinalIgnoreCase)))
        {
            var parameters = ParseIniLike(module.Text("params"));
            var paths = SplitLegacyPlaylist(parameters.GetValueOrDefault("vp_mp3"));
            var titles = SplitLegacyPlaylist(parameters.GetValueOrDefault("vp_title"));
            for (var index = 0; index < paths.Length; index++)
            {
                var legacyId = -(module.Int("id") * 100 + index + 1);
                var item = await db.ContentItems.SingleOrDefaultAsync(x => x.LegacyJoomlaId == legacyId, cancellationToken);
                if (item is null)
                {
                    item = new ContentItem { LegacyJoomlaId = legacyId, CreatedAt = DateTimeOffset.UnixEpoch };
                    db.ContentItems.Add(item);
                }
                var title = index < titles.Length && !string.IsNullOrWhiteSpace(titles[index]) ? titles[index] : module.Text("title");
                item.Title = title;
                item.Slug = $"audio-{Math.Abs(legacyId)}-{SlugGenerator.Generate(title)}";
                item.Content = string.Empty;
                item.CategoryId = category.Id;
                item.Type = ContentType.AudioPoem;
                item.IsPublished = module.Bool("published");
                item.PublishedAt = null;
                item.UpdatedAt = DateTimeOffset.UtcNow;
                item.SortOrder = index;
                item.AudioPath = ResolveMediaPath(paths[index]);
                count++;
            }
        }
        await db.SaveChangesAsync(cancellationToken);
        return count;
    }

    private async Task<int> MigrateLinksAsync(ApplicationDbContext db,
        IReadOnlyDictionary<string, List<JoomlaRow>> data, CancellationToken cancellationToken)
    {
        var count = 0;
        foreach (var source in data["jos_weblinks"])
        {
            var legacyId = source.Int("id");
            var url = source.Text("url").Trim();
            if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) || parsed.Scheme is not ("http" or "https"))
            {
                _warnings.Add($"Externe link {legacyId} overgeslagen wegens ongeldige URL.");
                continue;
            }
            var link = await db.ExternalLinks.SingleOrDefaultAsync(x => x.LegacyJoomlaId == legacyId, cancellationToken);
            if (link is null) { link = new ExternalLink { LegacyJoomlaId = legacyId }; db.ExternalLinks.Add(link); }
            link.Title = FirstNonEmpty(source.Text("title"), url);
            link.Url = url;
            link.Description = Limit(PlainText(source.Text("description")), 2_000);
            link.SortOrder = source.Int("ordering");
            link.IsVisible = source.Bool("published");
            count++;
        }
        await db.SaveChangesAsync(cancellationToken);
        return count;
    }

    private async Task<int> MigrateContactsAsync(ApplicationDbContext db,
        IReadOnlyDictionary<string, List<JoomlaRow>> data, CancellationToken cancellationToken)
    {
        var count = 0;
        foreach (var source in data["jos_contact_details"])
        {
            var legacyId = source.Int("id");
            var contact = await db.ContactInfos.SingleOrDefaultAsync(x => x.LegacyJoomlaId == legacyId, cancellationToken);
            if (contact is null) { contact = new ContactInfo { LegacyJoomlaId = legacyId }; db.ContactInfos.Add(contact); }
            contact.Name = source.Text("name");
            contact.Email = source.Get("email_to")?.Trim();
            contact.Telephone = source.Get("telephone")?.Trim();
            contact.Mobile = source.Get("mobile")?.Trim();
            contact.Address = string.Join("، ", new[] { source.Get("address"), source.Get("suburb"), source.Get("state"), source.Get("country") }.Where(x => !string.IsNullOrWhiteSpace(x)));
            contact.AdditionalInfo = _sanitizer.Sanitize(RewriteMedia(source.Text("misc")));
            contact.IsVisible = source.Bool("published");
            count++;
        }
        await db.SaveChangesAsync(cancellationToken);
        return count;
    }

    private async Task<int> MigrateRedirectsAsync(ApplicationDbContext db,
        IReadOnlyDictionary<string, List<JoomlaRow>> data, IReadOnlyDictionary<int, Category> categories,
        CancellationToken cancellationToken)
    {
        var redirects = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["/index.php?option=com_content&view=frontpage&Itemid=1"] = "/",
            ["/index.php?option=com_weblinks&view=category&id=16&Itemid=21"] = "/links",
            ["/index.php?option=com_contact&view=contact&id=1&Itemid=9"] = "/contact"
        };
        foreach (var menu in data["jos_menu"].Where(x => x.Bool("published")))
        {
            var link = WebUtility.HtmlDecode(menu.Text("link")).TrimStart('/');
            if (!link.StartsWith("index.php?", StringComparison.OrdinalIgnoreCase)) continue;
            var source = "/" + link + (link.Contains("Itemid=", StringComparison.OrdinalIgnoreCase) ? "" : $"&Itemid={menu.Int("id")}");
            var categoryMatch = Regex.Match(link, @"(?:\?|&)id=(\d+)", RegexOptions.IgnoreCase);
            if (link.Contains("view=category", StringComparison.OrdinalIgnoreCase) && categoryMatch.Success && categories.TryGetValue(int.Parse(categoryMatch.Groups[1].Value), out var category))
                redirects[source] = $"/category/{category.Slug}";
        }
        foreach (var content in await db.ContentItems.AsNoTracking().Where(x => x.LegacyJoomlaId > 0).ToListAsync(cancellationToken))
            redirects[$"/index.php?option=com_content&view=article&id={content.LegacyJoomlaId}"] = $"/content/{content.Slug}";

        foreach (var pair in redirects)
        {
            var entity = await db.LegacyRedirects.SingleOrDefaultAsync(x => x.Source == pair.Key, cancellationToken);
            if (entity is null) { entity = new LegacyRedirect { Source = pair.Key }; db.LegacyRedirects.Add(entity); }
            entity.Destination = pair.Value;
            entity.IsPermanent = true;
        }
        await db.SaveChangesAsync(cancellationToken);
        return redirects.Count;
    }

    private async Task<int> MigrateMediaAsync(ApplicationDbContext db,
        IReadOnlyDictionary<string, List<JoomlaRow>> data, CancellationToken cancellationToken)
    {
        var sourceFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddFiles(Path.Combine(publicRoot, "images"), SearchOption.TopDirectoryOnly, sourceFiles);
        AddFiles(Path.Combine(publicRoot, "images", "stories"), SearchOption.TopDirectoryOnly, sourceFiles);
        AddFiles(Path.Combine(publicRoot, "mp3"), SearchOption.TopDirectoryOnly, sourceFiles);
        foreach (var row in data["jos_content"].Concat(data["jos_contact_details"]))
        {
            foreach (Match match in Regex.Matches(row.Text("introtext") + row.Text("fulltext") + row.Text("misc"),
                         "(?:src|href)\\s*=\\s*[\"']([^\"']+)[\"']", RegexOptions.IgnoreCase))
            {
                var path = ResolveSourceFile(match.Groups[1].Value);
                if (path is not null) sourceFiles.Add(path);
            }
        }
        foreach (var module in data["jos_modules"])
        {
            foreach (Match match in Regex.Matches(module.Text("params"), @"(?:^|[=|])(/?(?:images|mp3)/[^|\r\n]+)", RegexOptions.IgnoreCase))
            {
                var path = ResolveSourceFile(match.Groups[1].Value);
                if (path is not null) sourceFiles.Add(path);
            }
        }

        var existing = (await db.MediaItems.Where(x => x.LegacyPath != null).ToListAsync(cancellationToken))
            .ToDictionary(x => x.LegacyPath!, StringComparer.OrdinalIgnoreCase);
        foreach (var sourcePath in sourceFiles.OrderBy(x => x))
        {
            var relative = Path.GetRelativePath(publicRoot, sourcePath).Replace('\\', '/');
            var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
            var mime = MimeType(extension);
            if (mime is null) continue;
            var group = mime.StartsWith("image/") ? "images/legacy" : mime.StartsWith("audio/") ? "audio" : "documents";
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(relative)))[..8].ToLowerInvariant();
            var safeBase = SafeFileName(Path.GetFileNameWithoutExtension(sourcePath));
            var fileName = $"{safeBase}-{hash}{extension}";
            var destinationDirectory = Path.Combine(webRoot, "uploads", group.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(destinationDirectory);
            var destination = Path.Combine(destinationDirectory, fileName);
            if (!File.Exists(destination)) File.Copy(sourcePath, destination, false);
            var publicPath = $"/uploads/{group}/{fileName}";
            _mediaMap[relative] = publicPath;
            _mediaMap["/" + relative] = publicPath;
            if (!existing.TryGetValue(relative, out var item))
            {
                item = new MediaItem { LegacyPath = relative };
                db.MediaItems.Add(item);
                existing[relative] = item;
            }
            item.FileName = fileName;
            item.OriginalFileName = Path.GetFileName(sourcePath);
            item.Path = publicPath;
            item.MimeType = mime;
            item.Size = new FileInfo(sourcePath).Length;
            item.AltText ??= Path.GetFileNameWithoutExtension(sourcePath);
        }
        await db.SaveChangesAsync(cancellationToken);
        return sourceFiles.Count;
    }

    private string RewriteMedia(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        return Regex.Replace(html, "(?<prefix>(?:src|href)\\s*=\\s*[\"'])(?<path>[^\"']+)(?<suffix>[\"'])", match =>
        {
            var replacement = ResolveMediaPath(WebUtility.HtmlDecode(match.Groups["path"].Value));
            return replacement is null ? match.Value : match.Groups["prefix"].Value + replacement + match.Groups["suffix"].Value;
        }, RegexOptions.IgnoreCase);
    }

    private static string RewriteLegacyYouTubeObjects(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        return Regex.Replace(html, @"<object\b.*?</object>", match =>
        {
            var video = Regex.Match(WebUtility.HtmlDecode(match.Value),
                @"youtube(?:-nocookie)?\.com/v/([A-Za-z0-9_-]{11})", RegexOptions.IgnoreCase);
            return video.Success
                ? $"<p><a href=\"https://www.youtube.com/watch?v={video.Groups[1].Value}\">مشاهدة الفيديو</a></p>"
                : string.Empty;
        }, RegexOptions.IgnoreCase | RegexOptions.Singleline);
    }

    private string? ResolveMediaPath(string raw)
    {
        var normalized = NormalizeLegacyWebPath(raw);
        if (normalized is null) return null;
        return _mediaMap.GetValueOrDefault(normalized) ?? _mediaMap.GetValueOrDefault("/" + normalized);
    }

    private string? ResolveSourceFile(string raw)
    {
        var normalized = NormalizeLegacyWebPath(raw);
        if (normalized is null) return null;
        var candidate = Path.GetFullPath(Path.Combine(publicRoot, normalized.Replace('/', Path.DirectorySeparatorChar)));
        return candidate.StartsWith(Path.GetFullPath(publicRoot), StringComparison.OrdinalIgnoreCase) && File.Exists(candidate) ? candidate : null;
    }

    private static string? NormalizeLegacyWebPath(string raw)
    {
        var value = raw.Trim().Replace('\\', '/');
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            if (!uri.Host.EndsWith("balkishassan.com", StringComparison.OrdinalIgnoreCase)) return null;
            value = uri.AbsolutePath;
        }
        value = value.Split('?', '#')[0].TrimStart('/');
        try { value = Uri.UnescapeDataString(value); } catch (UriFormatException) { }
        return value.StartsWith("images/", StringComparison.OrdinalIgnoreCase) || value.StartsWith("mp3/", StringComparison.OrdinalIgnoreCase)
            ? value : null;
    }

    private static void AddFiles(string directory, SearchOption option, ISet<string> output)
    {
        if (!Directory.Exists(directory)) return;
        foreach (var file in Directory.EnumerateFiles(directory, "*", option).Where(x => MimeType(Path.GetExtension(x).ToLowerInvariant()) is not null)) output.Add(file);
    }

    private static string? FindAsset(string html, string tag, string attribute, string mimePrefix)
    {
        var match = Regex.Match(html, $"<{tag}[^>]+{attribute}=[\"']([^\"']+)[\"']", RegexOptions.IgnoreCase);
        return match.Success && match.Groups[1].Value.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase) ? match.Groups[1].Value : null;
    }

    private static string? FindByExtension(string html, params string[] extensions)
    {
        var match = Regex.Match(html, "(?:src|href)=[\"']([^\"']+)[\"']", RegexOptions.IgnoreCase);
        while (match.Success)
        {
            if (extensions.Any(x => match.Groups[1].Value.EndsWith(x, StringComparison.OrdinalIgnoreCase))) return match.Groups[1].Value;
            match = match.NextMatch();
        }
        return null;
    }

    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.UnionWith(["p", "br", "strong", "b", "em", "i", "u", "blockquote", "ul", "ol", "li", "h2", "h3", "h4", "a", "img", "figure", "figcaption", "audio", "source", "span", "div", "table", "thead", "tbody", "tr", "th", "td"]);
        sanitizer.AllowedAttributes.UnionWith(["href", "src", "alt", "title", "controls", "preload", "class", "dir", "lang", "width", "height", "colspan", "rowspan"]);
        sanitizer.AllowedSchemes.UnionWith(["https", "http", "mailto"]);
        sanitizer.AllowDataAttributes = false;
        return sanitizer;
    }

    private static Dictionary<string, string> ParseIniLike(string value) => value.Split('\n')
        .Select(x => x.Trim()).Where(x => x.Contains('='))
        .Select(x => x.Split('=', 2)).GroupBy(x => x[0], StringComparer.OrdinalIgnoreCase)
        .ToDictionary(x => x.Key, x => x.Last()[1], StringComparer.OrdinalIgnoreCase);

    private static string[] SplitLegacyPlaylist(string? value) => string.IsNullOrWhiteSpace(value)
        ? []
        : Regex.Split(value, @"\\?\|").Select(x => x.Trim().TrimEnd('\\')).Where(x => x.Length > 0).ToArray();

    private static DateTimeOffset? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.StartsWith("0000-")) return null;
        return DateTime.TryParse(value, out var date)
            ? new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Unspecified), TimeSpan.FromHours(1)).ToUniversalTime()
            : null;
    }

    private static string PlainText(string html) => Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(html ?? string.Empty, "<[^>]+>", " ")), @"\s+", " ").Trim();
    private static string? Limit(string? value, int length) => string.IsNullOrWhiteSpace(value) ? null : value.Length <= length ? value : value[..length].TrimEnd();
    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
    private static string SafeFileName(string value)
    {
        var safe = new string(value.Normalize(NormalizationForm.FormKC).Select(x => char.IsLetterOrDigit(x) ? x : '-').ToArray());
        safe = Regex.Replace(safe, "-{2,}", "-").Trim('-').ToLowerInvariant();
        return string.IsNullOrWhiteSpace(safe) ? "bestand" : safe;
    }
    private static string? MimeType(string extension) => extension switch
    {
        ".jpg" or ".jpeg" => "image/jpeg", ".png" => "image/png", ".gif" => "image/gif", ".webp" => "image/webp",
        ".mp3" => "audio/mpeg", ".ogg" => "audio/ogg", ".wav" => "audio/wav", ".pdf" => "application/pdf", _ => null
    };
}

public sealed record MigrationReport(int Categories, int ContentItems, int ExternalLinks, int Contacts, int Media,
    int Redirects, IReadOnlyDictionary<string, int> SourceRows, IReadOnlyList<string> CategoryNames, int QuestionRuns, int ReplacementCharacters,
    IReadOnlyList<string> Warnings);
