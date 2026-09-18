using System.Globalization;
using BalkisHassan.Infrastructure;
using BalkisHassan.Web.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("De PostgreSQL-connection-string 'DefaultConnection' ontbreekt.");

builder.Services.AddBalkisInfrastructure(connectionString);
builder.Services.AddControllersWithViews();
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
app.Logger.LogInformation("Balkis Hassan webapplicatie start in omgeving {Environment}", app.Environment.EnvironmentName);
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
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found");
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

app.MapHealthChecks("/health");
app.MapControllerRoute("admin", "admin/{action=Index}/{id?}", new { controller = "Admin" });
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");

await DatabaseInitializer.InitializeAsync(app.Services, app.Configuration);
app.Run();

public partial class Program;
