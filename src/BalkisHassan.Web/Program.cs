using System.Globalization;
using System.Security;
using System.Text;
using BalkisHassan.Infrastructure;
using BalkisHassan.Web.Components;
using BalkisHassan.Web.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("De PostgreSQL-connection-string 'DefaultConnection' ontbreekt.");

builder.Services.AddBalkisInfrastructure(connectionString);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();
builder.Services.AddResponseCompression(options => options.EnableForHttps = true);
builder.Services.AddOutputCache();
builder.Services.AddHealthChecks().AddDbContextCheck<ApplicationDbContext>("postgresql");
builder.Services.AddSingleton<ContentSanitizer>();
builder.Services.AddScoped<MediaStorageService>();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

var app = builder.Build();
app.Logger.LogInformation("Balkis Hassan Blazor Web App start in omgeving {Environment}", app.Environment.EnvironmentName);
var arabicCulture = CultureInfo.GetCultureInfo("ar-IQ");
app.UseForwardedHeaders();
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(arabicCulture),
    SupportedCultures = [arabicCulture],
    SupportedUICultures = [arabicCulture]
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseResponseCompression();
app.UseHttpsRedirection();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "public,max-age=604800"
});
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseOutputCache();
app.UseMiddleware<LegacyRedirectMiddleware>();
app.UseAntiforgery();

app.MapPost("/admin/login/submit", async (HttpContext context, IAntiforgery antiforgery,
    SignInManager<ApplicationUser> signInManager) =>
{
    await antiforgery.ValidateRequestAsync(context);
    var form = await context.Request.ReadFormAsync(context.RequestAborted);
    var email = form["email"].ToString().Trim();
    var password = form["password"].ToString();
    var rememberMe = form["rememberMe"].ToString().Equals("true", StringComparison.OrdinalIgnoreCase);
    var returnUrl = SafeLocalUrl(form["returnUrl"].ToString()) ?? "/admin";
    var result = await signInManager.PasswordSignInAsync(email, password, rememberMe, true);
    return Results.Redirect(result.Succeeded ? returnUrl : $"/admin/login?error=1&returnUrl={Uri.EscapeDataString(returnUrl)}");
}).DisableAntiforgery();

app.MapPost("/admin/logout", async (HttpContext context, IAntiforgery antiforgery,
    SignInManager<ApplicationUser> signInManager) =>
{
    await antiforgery.ValidateRequestAsync(context);
    await signInManager.SignOutAsync();
    return Results.Redirect("/admin/login");
}).RequireAuthorization().DisableAntiforgery();

app.MapGet("/robots.txt", (HttpContext context) => Results.Text(
    $"User-agent: *\nAllow: /\nDisallow: /admin\nSitemap: {context.Request.Scheme}://{context.Request.Host}/sitemap.xml\n",
    "text/plain", Encoding.UTF8));

app.MapGet("/sitemap.xml", async (HttpContext context, ApplicationDbContext db, CancellationToken cancellationToken) =>
{
    var baseUrl = $"{context.Request.Scheme}://{context.Request.Host}";
    var urls = new List<string> { "/", "/links", "/contact" };
    urls.AddRange(await db.Categories.AsNoTracking().Where(x => x.IsVisible && x.LegacyJoomlaId != 6)
        .Select(x => "/category/" + x.Slug).ToListAsync(cancellationToken));
    urls.AddRange(await db.ContentItems.AsNoTracking()
        .Where(x => x.IsPublished && (x.PublishedAt == null || x.PublishedAt <= DateTimeOffset.UtcNow))
        .Select(x => "/content/" + x.Slug).ToListAsync(cancellationToken));
    var xml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n" +
              string.Join('\n', urls.Distinct().Select(x => $"<url><loc>{SecurityElement.Escape(baseUrl + x)}</loc></url>")) +
              "\n</urlset>";
    return Results.Text(xml, "application/xml", Encoding.UTF8);
}).CacheOutput(policy => policy.Expire(TimeSpan.FromHours(1)));

app.MapHealthChecks("/health");
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await DatabaseInitializer.InitializeAsync(app.Services, app.Configuration);
app.Run();

static string? SafeLocalUrl(string? value) =>
    !string.IsNullOrWhiteSpace(value) && value.StartsWith('/') && !value.StartsWith("//") ? value : null;

public partial class Program;
