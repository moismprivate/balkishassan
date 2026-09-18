using BalkisHassan.Domain;

namespace BalkisHassan.Web.Models;

public sealed record HomeViewModel(IReadOnlyList<ContentItem> Featured, IReadOnlyList<ContentItem> Latest,
    IReadOnlyList<Category> Categories, ContentItem? Hero, ContentItem? Biography, string? PortraitPath);
public sealed record CategoryViewModel(Category Category, IReadOnlyList<ContentItem> Items, int Page, int TotalPages);
public sealed record SearchViewModel(string Query, IReadOnlyList<ContentItem> Results);
