using BalkisHassan.Domain;
using BalkisHassan.Infrastructure;
using BalkisHassan.Web.Models;
using BalkisHassan.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BalkisHassan.Web.Controllers;

[Authorize]
[Route("admin")]
public sealed class AdminController(
    ApplicationDbContext db,
    SignInManager<ApplicationUser> signInManager,
    ContentSanitizer sanitizer,
    MediaStorageService mediaStorage,
    ILogger<AdminController> logger) : Controller
{
    [AllowAnonymous]
    [HttpGet("login")]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid) return View(model);
        var result = await signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, true);
        if (result.Succeeded) return LocalRedirect(string.IsNullOrWhiteSpace(returnUrl) ? "/admin" : returnUrl);
        ModelState.AddModelError(string.Empty, "البريد الإلكتروني أو كلمة المرور غير صحيحة.");
        return View(model);
    }

    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var published = await db.ContentItems.CountAsync(x => x.IsPublished, cancellationToken);
        var drafts = await db.ContentItems.CountAsync(x => !x.IsPublished, cancellationToken);
        return View(new AdminDashboardViewModel(published, drafts,
            await db.Categories.CountAsync(cancellationToken), await db.MediaItems.CountAsync(cancellationToken)));
    }

    [HttpGet("contents")]
    public async Task<IActionResult> Contents(string? q, int page = 1, CancellationToken cancellationToken = default)
    {
        const int pageSize = 25;
        page = Math.Max(page, 1);
        var queryText = q?.Trim() ?? string.Empty;
        var query = db.ContentItems.AsNoTracking().Include(x => x.Category).AsQueryable();
        if (!string.IsNullOrWhiteSpace(queryText)) query = query.Where(x => EF.Functions.ILike(x.Title, $"%{queryText}%"));
        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.UpdatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return View(new AdminContentListViewModel(items, queryText, page, Math.Max(1, (int)Math.Ceiling(count / (double)pageSize))));
    }

    [HttpGet("content/new")]
    public async Task<IActionResult> Create(ContentType type = ContentType.Article, CancellationToken cancellationToken = default)
    {
        await PopulateListsAsync(cancellationToken);
        return View("Edit", new ContentEditViewModel { Type = type, PublishedAt = DateTime.Now });
    }

    [HttpPost("content/new")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ContentEditViewModel model, CancellationToken cancellationToken)
    {
        if (!await db.Categories.AnyAsync(x => x.Id == model.CategoryId, cancellationToken))
            ModelState.AddModelError(nameof(model.CategoryId), "اختر تصنيفاً صحيحاً.");
        if (!ModelState.IsValid)
        {
            await PopulateListsAsync(cancellationToken);
            return View("Edit", model);
        }

        var item = new ContentItem { CreatedAt = DateTimeOffset.UtcNow };
        await ApplyAsync(item, model, cancellationToken);
        db.ContentItems.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Beheerder publiceerde of bewaarde inhoud {ContentId}: {Title}", item.Id, item.Title);
        TempData["Message"] = "تم حفظ المحتوى بنجاح.";
        return RedirectToAction(nameof(Contents));
    }

    [HttpGet("content/{id:int}")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var item = await db.ContentItems.FindAsync([id], cancellationToken);
        if (item is null) return NotFound();
        await PopulateListsAsync(cancellationToken);
        return View(new ContentEditViewModel
        {
            Id = item.Id, Title = item.Title, Slug = item.Slug, Summary = item.Summary, Content = item.Content,
            CategoryId = item.CategoryId, Type = item.Type, PublishedAt = item.PublishedAt?.LocalDateTime,
            IsPublished = item.IsPublished, Featured = item.Featured, SortOrder = item.SortOrder,
            MainImage = item.MainImage, AudioPath = item.AudioPath, DocumentPath = item.DocumentPath
        });
    }

    [HttpPost("content/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ContentEditViewModel model, CancellationToken cancellationToken)
    {
        if (id != model.Id) return BadRequest();
        var item = await db.ContentItems.FindAsync([id], cancellationToken);
        if (item is null) return NotFound();
        if (!await db.Categories.AnyAsync(x => x.Id == model.CategoryId, cancellationToken))
            ModelState.AddModelError(nameof(model.CategoryId), "اختر تصنيفاً صحيحاً.");
        if (!ModelState.IsValid)
        {
            await PopulateListsAsync(cancellationToken);
            return View(model);
        }

        await ApplyAsync(item, model, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Beheerder wijzigde inhoud {ContentId}: {Title}", item.Id, item.Title);
        TempData["Message"] = "تم تحديث المحتوى بنجاح.";
        return RedirectToAction(nameof(Contents));
    }

    [HttpGet("content/{id:int}/delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var item = await db.ContentItems.AsNoTracking().Include(x => x.Category)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return item is null ? NotFound() : View(item);
    }

    [HttpPost("content/{id:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken cancellationToken)
    {
        var item = await db.ContentItems.FindAsync([id], cancellationToken);
        if (item is null) return NotFound();
        db.ContentItems.Remove(item);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogWarning("Beheerder verwijderde inhoud {ContentId}: {Title}", item.Id, item.Title);
        TempData["Message"] = "تم حذف المحتوى.";
        return RedirectToAction(nameof(Contents));
    }

    [HttpGet("categories")]
    public async Task<IActionResult> Categories(CancellationToken cancellationToken) =>
        View(await db.Categories.AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync(cancellationToken));

    [HttpPost("category/save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveCategory(int id, string name, string? slug, string? description, int sortOrder, bool isVisible,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name)) return RedirectToAction(nameof(Categories));
        var category = id == 0 ? new Category() : await db.Categories.FindAsync([id], cancellationToken);
        if (category is null) return NotFound();
        category.Name = name.Trim();
        category.Slug = await UniqueCategorySlugAsync(slug, name, id, cancellationToken);
        category.Description = description?.Trim();
        category.SortOrder = sortOrder;
        category.IsVisible = isVisible;
        if (id == 0) db.Categories.Add(category);
        await db.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Categories));
    }

    [HttpGet("media")]
    public async Task<IActionResult> Media(CancellationToken cancellationToken) =>
        View(new AdminMediaViewModel(await db.MediaItems.AsNoTracking().OrderByDescending(x => x.CreatedAt).Take(250).ToListAsync(cancellationToken), null));

    [HttpPost("media")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(26 * 1024 * 1024)]
    public async Task<IActionResult> Media(IFormFile? file, string? altText, CancellationToken cancellationToken)
    {
        if (file is null)
            return View(new AdminMediaViewModel(await db.MediaItems.AsNoTracking().OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken), "اختر ملفاً."));
        try
        {
            var media = await mediaStorage.SaveAsync(file, altText, cancellationToken);
            db.MediaItems.Add(media);
            await db.SaveChangesAsync(cancellationToken);
            TempData["Message"] = "تم رفع الملف بنجاح.";
            return RedirectToAction(nameof(Media));
        }
        catch (InvalidOperationException exception)
        {
            logger.LogWarning("Media-upload geweigerd: {Reason}", exception.Message);
            return View(new AdminMediaViewModel(await db.MediaItems.AsNoTracking().OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken), exception.Message));
        }
    }

    private async Task ApplyAsync(ContentItem item, ContentEditViewModel model, CancellationToken cancellationToken)
    {
        item.Title = model.Title.Trim();
        item.Slug = await UniqueContentSlugAsync(model.Slug, model.Title, item.Id, cancellationToken);
        item.Summary = string.IsNullOrWhiteSpace(model.Summary) ? null : sanitizer.Sanitize(model.Summary.Trim());
        item.Content = sanitizer.Sanitize(model.Content);
        item.CategoryId = model.CategoryId;
        item.Type = model.Type;
        item.PublishedAt = model.PublishedAt is null ? null : new DateTimeOffset(model.PublishedAt.Value.ToUniversalTime());
        item.IsPublished = model.IsPublished;
        item.Featured = model.Featured;
        item.SortOrder = model.SortOrder;
        item.MainImage = NormalizeMediaPath(model.MainImage);
        item.AudioPath = NormalizeMediaPath(model.AudioPath);
        item.DocumentPath = NormalizeMediaPath(model.DocumentPath);
        item.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private async Task PopulateListsAsync(CancellationToken cancellationToken)
    {
        ViewBag.Categories = new SelectList(await db.Categories.AsNoTracking().OrderBy(x => x.SortOrder).ToListAsync(cancellationToken), "Id", "Name");
        ViewBag.Media = await db.MediaItems.AsNoTracking().OrderByDescending(x => x.CreatedAt).Take(250).ToListAsync(cancellationToken);
    }

    private async Task<string> UniqueContentSlugAsync(string? requested, string title, int currentId, CancellationToken cancellationToken)
    {
        var basis = SlugGenerator.Generate(string.IsNullOrWhiteSpace(requested) ? title : requested);
        var candidate = basis;
        var suffix = 2;
        while (await db.ContentItems.AnyAsync(x => x.Slug == candidate && x.Id != currentId, cancellationToken)) candidate = $"{basis}-{suffix++}";
        return candidate;
    }

    private async Task<string> UniqueCategorySlugAsync(string? requested, string name, int currentId, CancellationToken cancellationToken)
    {
        var basis = SlugGenerator.Generate(string.IsNullOrWhiteSpace(requested) ? name : requested);
        var candidate = basis;
        var suffix = 2;
        while (await db.Categories.AnyAsync(x => x.Slug == candidate && x.Id != currentId, cancellationToken)) candidate = $"{basis}-{suffix++}";
        return candidate;
    }

    private static string? NormalizeMediaPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var path = value.Trim();
        return path.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase) ? path : null;
    }
}
