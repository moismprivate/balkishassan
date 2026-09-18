using BalkisHassan.Domain;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BalkisHassan.Infrastructure;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<ContentItem> ContentItems => Set<ContentItem>();
    public DbSet<MediaItem> MediaItems => Set<MediaItem>();
    public DbSet<ExternalLink> ExternalLinks => Set<ExternalLink>();
    public DbSet<ContactInfo> ContactInfos => Set<ContactInfo>();
    public DbSet<LegacyRedirect> LegacyRedirects => Set<LegacyRedirect>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>().Property(x => x.DisplayName).HasMaxLength(200);

        builder.Entity<Category>(entity =>
        {
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.HasIndex(x => x.LegacyJoomlaId).IsUnique();
        });

        builder.Entity<ContentItem>(entity =>
        {
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.HasIndex(x => x.LegacyJoomlaId).IsUnique();
            entity.HasIndex(x => new { x.CategoryId, x.IsPublished, x.PublishedAt });
            entity.HasOne(x => x.Category).WithMany(x => x.ContentItems).HasForeignKey(x => x.CategoryId);
        });

        builder.Entity<MediaItem>(entity =>
        {
            entity.HasIndex(x => x.Path).IsUnique();
            entity.HasIndex(x => x.LegacyPath);
        });

        builder.Entity<ExternalLink>().HasIndex(x => x.LegacyJoomlaId).IsUnique();
        builder.Entity<ContactInfo>().HasIndex(x => x.LegacyJoomlaId).IsUnique();
        builder.Entity<LegacyRedirect>().HasIndex(x => x.Source).IsUnique();
    }
}
