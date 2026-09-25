using Microsoft.EntityFrameworkCore;
using Cms.Studio.Core.Domain;

namespace Cms.Studio.Core.Data;

public class CmsDbContext : DbContext
{
    public CmsDbContext(DbContextOptions<CmsDbContext> options) : base(options)
    {
    }

    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<PostTag> PostTags => Set<PostTag>();
    public DbSet<Series> Series => Set<Series>();
    public DbSet<PostSeries> PostSeries => Set<PostSeries>();
    public DbSet<PostViewDaily> PostViewDaily => Set<PostViewDaily>();
    public DbSet<VisitRecord> VisitRecords => Set<VisitRecord>();
    public DbSet<SlugHistory> SlugHistory => Set<SlugHistory>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<Comment> Comments => Set<Comment>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<Post>(entity =>
        {
            entity.ToTable("Post");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Slug).HasMaxLength(200).IsRequired();
            entity.HasIndex(e => e.Slug).IsUnique();
            entity.Property(e => e.Title).HasMaxLength(300).IsRequired();
            entity.Property(e => e.AuthorName).HasMaxLength(100);
            entity.Property(e => e.MetaTitle).HasMaxLength(300);
            entity.Property(e => e.MetaDescription).HasMaxLength(500);
            entity.Property(e => e.Status).HasConversion<int>();
            entity.HasOne(e => e.Category)
                .WithMany(e => e.Posts)
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Category>(entity =>
        {
            entity.ToTable("Category");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(150).IsRequired();
            entity.Property(e => e.Slug).HasMaxLength(200).IsRequired();
            entity.HasIndex(e => e.Slug).IsUnique();
            entity.HasOne(e => e.ParentCategory)
                .WithMany()
                .HasForeignKey(e => e.ParentCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Tag>(entity =>
        {
            entity.ToTable("Tag");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Slug).HasMaxLength(200).IsRequired();
            entity.HasIndex(e => e.Slug).IsUnique();
        });

        builder.Entity<PostTag>(entity =>
        {
            entity.ToTable("PostTag");
            entity.HasKey(e => new { e.PostId, e.TagId });
            entity.HasOne(e => e.Post).WithMany(e => e.PostTags).HasForeignKey(e => e.PostId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Tag).WithMany(e => e.PostTags).HasForeignKey(e => e.TagId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Series>(entity =>
        {
            entity.ToTable("Series");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(300).IsRequired();
            entity.Property(e => e.Slug).HasMaxLength(200).IsRequired();
            entity.HasIndex(e => e.Slug).IsUnique();
        });

        builder.Entity<PostSeries>(entity =>
        {
            entity.ToTable("PostSeries");
            entity.HasKey(e => new { e.PostId, e.SeriesId });
            entity.HasOne(e => e.Post).WithMany(e => e.PostSeries).HasForeignKey(e => e.PostId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Series).WithMany(e => e.PostSeries).HasForeignKey(e => e.SeriesId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PostViewDaily>(entity =>
        {
            entity.ToTable("PostViewDaily");
            entity.HasKey(e => new { e.PostId, e.Date });
            entity.HasOne(e => e.Post).WithMany().HasForeignKey(e => e.PostId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<VisitRecord>(entity =>
        {
            entity.ToTable("VisitRecord");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Ip).HasMaxLength(64).IsRequired();
            entity.Property(e => e.UserAgent).HasMaxLength(500);
            entity.Property(e => e.Browser).HasMaxLength(60);
            entity.Property(e => e.Platform).HasMaxLength(60);
            entity.Property(e => e.Device).HasMaxLength(20);
            entity.Property(e => e.Path).HasMaxLength(300);
            entity.Property(e => e.Referer).HasMaxLength(300);
            entity.HasIndex(e => e.VisitedOnUtc);
            entity.HasIndex(e => new { e.Ip, e.VisitedOnUtc });
        });

        builder.Entity<SlugHistory>(entity =>
        {
            entity.ToTable("SlugHistory");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OldSlug).HasMaxLength(200).IsRequired();
            entity.HasIndex(e => e.OldSlug).IsUnique();
        });

        builder.Entity<Setting>(entity =>
        {
            entity.ToTable("Setting");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Key).HasMaxLength(150).IsRequired();
            entity.HasIndex(e => e.Key).IsUnique();
        });

        builder.Entity<Comment>(entity =>
        {
            entity.ToTable("Comment");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.NickName).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Content).HasMaxLength(2000).IsRequired();
            entity.HasIndex(e => new { e.PostId, e.IsApproved });
            entity.HasOne<Post>()
                .WithMany()
                .HasForeignKey(e => e.PostId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
