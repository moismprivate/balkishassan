using System.ComponentModel.DataAnnotations;
using BalkisHassan.Domain;

namespace BalkisHassan.Web.Models;

public sealed class LoginViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
}

public sealed class ContentEditViewModel
{
    public int Id { get; set; }

    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(320)]
    public string? Slug { get; set; }

    [MaxLength(2_000)]
    public string? Summary { get; set; }

    [Required]
    public string Content { get; set; } = string.Empty;

    [Required]
    public int CategoryId { get; set; }

    public ContentType Type { get; set; } = ContentType.Article;
    public DateTime? PublishedAt { get; set; } = DateTime.Now;
    public bool IsPublished { get; set; }
    public bool Featured { get; set; }
    public int SortOrder { get; set; }

    [MaxLength(500)]
    public string? MainImage { get; set; }

    [MaxLength(500)]
    public string? AudioPath { get; set; }

    [MaxLength(500)]
    public string? DocumentPath { get; set; }
}

public sealed record AdminDashboardViewModel(int Published, int Drafts, int Categories, int Media);
public sealed record AdminContentListViewModel(IReadOnlyList<ContentItem> Items, string Query, int Page, int TotalPages);
public sealed record AdminMediaViewModel(IReadOnlyList<MediaItem> Items, string? Error);
