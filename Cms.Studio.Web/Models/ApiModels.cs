namespace Cms.Studio.Web.Models;

/// <summary>
/// JSON body for <c>POST /api/posts</c> and <c>PUT /api/posts/{id}</c>.
/// On update, omitted (null) fields keep their current values — except Tags / SeriesIds,
/// where omitting the field also keeps the current ones (send an empty array to clear).
/// </summary>
public class ApiPostInput
{
    /// <summary>Required on create.</summary>
    public string? Title { get; set; }

    /// <summary>Optional — generated from the title when omitted.</summary>
    public string? Slug { get; set; }

    public string? Summary { get; set; }

    /// <summary>Article body in Markdown. Required on create.</summary>
    public string? ContentMd { get; set; }

    /// <summary>"published" (default) or "draft".</summary>
    public string? Status { get; set; }

    /// <summary>Existing category id. Either this or CategoryName is required on create.</summary>
    public int? CategoryId { get; set; }

    /// <summary>Category name — found by slug, or created automatically when new.</summary>
    public string? CategoryName { get; set; }

    /// <summary>Tag names (created automatically when new).</summary>
    public List<string>? Tags { get; set; }

    /// <summary>Existing series ids to attach the post to.</summary>
    public List<int>? SeriesIds { get; set; }

    public string? CoverImageUrl { get; set; }
    public string? AuthorName { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public bool? IsFixedTop { get; set; }

    /// <summary>ISO-8601 publish time — defaults to now when publishing.</summary>
    public DateTime? PublishedOnUtc { get; set; }
}

/// <summary>JSON body for <c>POST /api/images/base64</c>.</summary>
public class ApiImageInput
{
    /// <summary>File name incl. extension — decides the stored type (jpg / png / gif / webp / …).</summary>
    public string? FileName { get; set; }

    /// <summary>The image bytes, base64-encoded.</summary>
    public string? DataBase64 { get; set; }
}
