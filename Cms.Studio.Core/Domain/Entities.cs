namespace Cms.Studio.Core.Domain;

/// <summary>Editorial status of a post.</summary>
public enum PostStatus
{
    Draft = 0,
    Published = 1
}

/// <summary>Ranking window used by the daily / weekly / monthly leaderboards.</summary>
public enum RankPeriod
{
    Daily,
    Weekly,
    Monthly
}

public class Post
{
    public int Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string ContentMd { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public PostStatus Status { get; set; } = PostStatus.Draft;
    public bool IsFixedTop { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public int ViewCount { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? PublishedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public ICollection<PostTag> PostTags { get; set; } = new List<PostTag>();
    public ICollection<PostSeries> PostSeries { get; set; } = new List<PostSeries>();
}

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? ParentCategoryId { get; set; }
    public Category? ParentCategory { get; set; }
    public int DisplayOrder { get; set; }
    public bool ShowOnMenu { get; set; } = true;
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public ICollection<Post> Posts { get; set; } = new List<Post>();
}

public class Tag
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public ICollection<PostTag> PostTags { get; set; } = new List<PostTag>();
}

public class PostTag
{
    public int PostId { get; set; }
    public Post Post { get; set; } = null!;
    public int TagId { get; set; }
    public Tag Tag { get; set; } = null!;
}

public class Series
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CoverImageUrl { get; set; }
    public int DisplayOrder { get; set; }
    public ICollection<PostSeries> PostSeries { get; set; } = new List<PostSeries>();
}

public class PostSeries
{
    public int PostId { get; set; }
    public Post Post { get; set; } = null!;
    public int SeriesId { get; set; }
    public Series Series { get; set; } = null!;
    public int DisplayOrder { get; set; }
}

/// <summary>Per-day view rollup. Feeds the daily / weekly / monthly leaderboards and the dashboard trend.</summary>
public class PostViewDaily
{
    public int PostId { get; set; }
    public Post? Post { get; set; }
    /// <summary>UTC date (time component is midnight).</summary>
    public DateTime Date { get; set; }
    public int Views { get; set; }
}

/// <summary>Keeps old slugs so renamed posts can answer with a 301 for SEO.</summary>
public class SlugHistory
{
    public int Id { get; set; }
    public int PostId { get; set; }
    public string OldSlug { get; set; } = string.Empty;
    public DateTime ChangedOnUtc { get; set; }
}

public class Setting
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

/// <summary>Reader comment. Created unapproved (pending moderation), verified by email code.</summary>
public class Comment
{
    public int Id { get; set; }
    public int PostId { get; set; }
    /// <summary>Parent comment id for threaded replies (one level shown).</summary>
    public int? ParentId { get; set; }
    public string NickName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedOnUtc { get; set; }
    public bool IsApproved { get; set; }
}
