using BalkisHassan.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BalkisHassan.Web.Services;

public sealed class LegacyRedirectMiddleware(RequestDelegate next)
{
    private static readonly IReadOnlyDictionary<string, string> MenuRedirects = new Dictionary<string, string>
    {
        ["1"] = "/", ["23"] = "/category/قصائد-بصوت-الشاعرة", ["2"] = "/content/أعمال-الشاعرة",
        ["3"] = "/category/من-مقالات-الشاعرة", ["18"] = "/category/محطات", ["4"] = "/category/كتب-عن-الشاعرة",
        ["5"] = "/category/قصائد", ["11"] = "/category/لقاءات-مع-الشاعرة", ["21"] = "/links", ["9"] = "/contact"
    };

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext db)
    {
        if (context.Request.Path.Equals("/category/كتب-بلقيس", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Redirect(EncodeLocation("/content/أعمال-الشاعرة"), true);
            return;
        }

        if (context.Request.Path.Equals("/index.php", StringComparison.OrdinalIgnoreCase) ||
            context.Request.Path.Equals("/content/index.php", StringComparison.OrdinalIgnoreCase))
        {
            var idText = context.Request.Query["id"].ToString().Split(':')[0];
            var isArticle = context.Request.Query["view"].ToString().Equals("article", StringComparison.OrdinalIgnoreCase);
            if (isArticle && int.TryParse(idText, out var legacyId))
            {
                var slug = await db.ContentItems.AsNoTracking().Where(x => x.LegacyJoomlaId == legacyId)
                    .Select(x => x.Slug).SingleOrDefaultAsync(context.RequestAborted);
                if (slug is not null)
                {
                    context.Response.Redirect($"/content/{Uri.EscapeDataString(slug)}", true);
                    return;
                }
            }

            var itemId = context.Request.Query["Itemid"].ToString();
            if (MenuRedirects.TryGetValue(itemId, out var menuDestination))
            {
                context.Response.Redirect(EncodeLocation(menuDestination), true);
                return;
            }
        }

        var source = $"{context.Request.Path}{context.Request.QueryString}";
        var redirect = await db.LegacyRedirects.AsNoTracking().SingleOrDefaultAsync(x => x.Source == source, context.RequestAborted);
        if (redirect is not null)
        {
            context.Response.Redirect(EncodeLocation(redirect.Destination), redirect.IsPermanent);
            return;
        }

        await next(context);
    }

    private static string EncodeLocation(string destination)
    {
        var queryStart = destination.IndexOf('?');
        var path = queryStart < 0 ? destination : destination[..queryStart];
        var query = queryStart < 0 ? string.Empty : destination[queryStart..];
        return new PathString(path).ToUriComponent() + query;
    }
}
