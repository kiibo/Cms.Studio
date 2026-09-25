using Cms.Studio.Core.Domain;
using Cms.Studio.Core.Services;

namespace Cms.Studio.Web.Models;
/// <summary>List page (home feed / category / tag / series / archive / search).</summary>
public class ListViewModel
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>Relative canonical URL, e.g. "/category/ai".</summary>
    public string CanonicalUrl { get; set; } = "/";
    public PagedResult<Post> Posts { get; set; } = new(new List<Post>(), 1, 10, 0);
}

public class PostViewModel
{
    public Post Post { get; set; } = null!;
    public string HtmlContent { get; set; } = string.Empty;
    public List<Post> Related { get; set; } = new();

    // ---------- comments ----------
    public List<CommentViewModel> Comments { get; set; } = new();
    public int CommentCount { get; set; }
    public CommentFormModel CommentForm { get; set; } = new();
    /// <summary>Success / info notice shown above the comment form.</summary>
    public string? CommentNotice { get; set; }
    /// <summary>Set when the form is replying to an existing comment.</summary>
    public Comment? ReplyingTo { get; set; }
}

/// <summary>Comment display node (top-level comment with one level of replies).</summary>
public class CommentViewModel
{
    public Comment Comment { get; set; } = null!;
    public List<CommentViewModel> Replies { get; set; } = new();
}

/// <summary>Guest comment form (email + verification code required, masuit.blog-style).</summary>
public class CommentFormModel
{
    public string NickName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int? ParentId { get; set; }
}

public class RankViewModel
{
    public RankPeriod Period { get; set; }
    public List<RankItem> Items { get; set; } = new();
}

public class SidebarViewModel
{
    public List<RankItem> Daily { get; set; } = new();
    public List<RankItem> Weekly { get; set; } = new();
    public List<RankItem> Monthly { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
    public List<TagWithCount> TopTags { get; set; } = new();
    public List<Series> Series { get; set; } = new();
}

// ---------- admin ----------

public class LoginViewModel
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Error { get; set; }
}

public class AdminDashboardViewModel
{
    public DashboardStats Stats { get; set; } = new();
    public List<DailyViews> Trend { get; set; } = new();
    public List<RankItem> TopPosts { get; set; } = new();
    /// <summary>Unique visitors per day (same IP + same day counts once).</summary>
    public List<DailyVisits> VisitorTrend { get; set; } = new();
    /// <summary>Visitor aggregates for the last 30 days.</summary>
    public VisitorStats Visitors { get; set; } = new();
}

public class AdminPostEditViewModel
{
    public Post Post { get; set; } = new();
    /// <summary>Comma-separated tag names.</summary>
    public string TagsInput { get; set; } = string.Empty;
    public List<int> SelectedSeriesIds { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
    public List<Series> AllSeries { get; set; } = new();
}
