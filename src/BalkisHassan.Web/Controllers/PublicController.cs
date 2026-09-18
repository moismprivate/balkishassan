using System.Text;
using BalkisHassan.Infrastructure;
using BalkisHassan.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BalkisHassan.Web.Controllers;

public sealed class HomeController(ApplicationDbContext db) : Controller
{
    [HttpGet("/")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var baseQuery = db.ContentItems.AsNoTracking().Include(x => x.Category)
            .Where(x => x.IsPublished && (x.PublishedAt == null || x.PublishedAt <= now) && x.Category.IsVisible);
        var latest = await baseQuery.OrderByDescending(x => x.PublishedAt.HasValue).ThenByDescending(x => x.PublishedAt).ThenByDescending(x => x.Id)
            .Take(12).ToListAsync(cancellationToken);
        var featured = await baseQuery.Where(x => x.Featured).OrderByDescending(x => x.PublishedAt.HasValue).ThenByDescending(x => x.PublishedAt)
            .Take(6).ToListAsync(cancellationToken);
        var categories = await db.Categories.AsNoTracking().Where(x => x.IsVisible)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync(cancellationToken);
        var hero = featured.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.MainImage))
                   ?? latest.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.MainImage)) ?? latest.FirstOrDefault();
        var biography = await db.ContentItems.AsNoTracking().Include(x => x.Category)
            .SingleOrDefaultAsync(x => x.LegacyJoomlaId == 6 && x.IsPublished, cancellationToken);
        var portraitPath = await db.MediaItems.AsNoTracking()
            .Where(x => x.LegacyPath != null && x.LegacyPath.ToLower() == "images/stories/balkis.jpg")
            .Select(x => x.Path)
            .FirstOrDefaultAsync(cancellationToken);
        return View(new HomeViewModel(featured, latest, categories, hero, biography, portraitPath));
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search(string? q, CancellationToken cancellationToken)
    {
        var query = q?.Trim() ?? string.Empty;
        IReadOnlyList<Domain.ContentItem> results = string.IsNullOrWhiteSpace(query)
            ? []
            : await db.ContentItems.AsNoTracking().Include(x => x.Category)
                .Where(x => x.IsPublished && (x.PublishedAt == null || x.PublishedAt <= DateTimeOffset.UtcNow) &&
                            (EF.Functions.ILike(x.Title, $"%{query}%") || EF.Functions.ILike(x.Content, $"%{query}%")))
                .OrderByDescending(x => x.PublishedAt.HasValue).ThenByDescending(x => x.PublishedAt).Take(50).ToListAsync(cancellationToken);
        return View(new SearchViewModel(query, results));
    }

    [HttpGet("links")]
    public async Task<IActionResult> Links(CancellationToken cancellationToken) =>
        View(await db.ExternalLinks.AsNoTracking().Where(x => x.IsVisible).OrderBy(x => x.SortOrder).ToListAsync(cancellationToken));

    [HttpGet("contact")]
    public async Task<IActionResult> Contact(CancellationToken cancellationToken) =>
        View(await db.ContactInfos.AsNoTracking().Where(x => x.IsVisible).OrderBy(x => x.Id).ToListAsync(cancellationToken));

    [HttpGet("robots.txt")]
    public ContentResult Robots() => Content($"User-agent: *\nAllow: /\nSitemap: {Request.Scheme}://{Request.Host}/sitemap.xml\n", "text/plain", Encoding.UTF8);

    [HttpGet("sitemap.xml")]
    [ResponseCache(Duration = 3600)]
    public async Task<ContentResult> Sitemap(CancellationToken cancellationToken)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var urls = new List<string> { "/", "/links", "/contact" };
        urls.AddRange(await db.Categories.AsNoTracking().Where(x => x.IsVisible)
            .Select(x => "/category/" + x.Slug).ToListAsync(cancellationToken));
        urls.AddRange(await db.ContentItems.AsNoTracking().Where(x => x.IsPublished && (x.PublishedAt == null || x.PublishedAt <= DateTimeOffset.UtcNow))
            .Select(x => "/content/" + x.Slug).ToListAsync(cancellationToken));
        var xml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n" +
                  string.Join("\n", urls.Select(x => $"<url><loc>{System.Security.SecurityElement.Escape(baseUrl + x)}</loc></url>")) +
                  "\n</urlset>";
        return Content(xml, "application/xml", Encoding.UTF8);
    }

    [HttpGet("error")]
    public IActionResult Error() => View();

    [HttpGet("not-found")]
    public IActionResult NotFoundPage()
    {
        Response.StatusCode = StatusCodes.Status404NotFound;
        return View("NotFound");
    }
}

public sealed class ContentController(ApplicationDbContext db) : Controller
{
    [HttpGet("category/{slug}")]
    public async Task<IActionResult> Category(string slug, int page = 1, CancellationToken cancellationToken = default)
    {
        const int pageSize = 12;
        page = Math.Max(1, page);
        var category = await db.Categories.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Slug == slug && x.IsVisible, cancellationToken);
        if (category is null) return RedirectToAction("NotFoundPage", "Home");

        var query = db.ContentItems.AsNoTracking().Include(x => x.Category)
            .Where(x => x.CategoryId == category.Id && x.IsPublished && (x.PublishedAt == null || x.PublishedAt <= DateTimeOffset.UtcNow));
        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.PublishedAt.HasValue).ThenByDescending(x => x.PublishedAt).ThenBy(x => x.SortOrder).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return View(new CategoryViewModel(category, items, page, Math.Max(1, (int)Math.Ceiling(count / (double)pageSize))));
    }

    [HttpGet("content/{slug}")]
    public async Task<IActionResult> Details(string slug, CancellationToken cancellationToken)
    {
        var item = await db.ContentItems.AsNoTracking().Include(x => x.Category)
            .SingleOrDefaultAsync(x => x.Slug == slug && x.IsPublished && (x.PublishedAt == null || x.PublishedAt <= DateTimeOffset.UtcNow), cancellationToken);
        return item is null ? RedirectToAction("NotFoundPage", "Home") : View(item);
    }
}
