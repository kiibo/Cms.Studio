using System.Text;
using System.Text.RegularExpressions;
using Markdig;

namespace Cms.Studio.Core.Services;

/// <summary>Markdown rendering, slug generation and small text helpers.</summary>
public class ContentService
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public string ToHtml(string markdown)
    {
        return string.IsNullOrWhiteSpace(markdown) ? string.Empty : Markdown.ToHtml(markdown, Pipeline);
    }

    /// <summary>ASCII slug: lowercase, alphanumeric words joined by dashes. Falls back to "post" when empty.</summary>
    public string Slugify(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return "post";

        var text = title.Trim().ToLowerInvariant();
        text = Regex.Replace(text, @"[^a-z0-9\s-]", string.Empty);
        text = Regex.Replace(text, @"\s+", "-");
        text = Regex.Replace(text, "-{2,}", "-").Trim('-');

        return string.IsNullOrWhiteSpace(text) ? "post" : text;
    }

    /// <summary>Plain-text excerpt used as the default summary / meta description.</summary>
    public string Excerpt(string markdown, int maxLength = 160)
    {
        var text = Regex.Replace(markdown ?? string.Empty, @"```.*?```", " ", RegexOptions.Singleline);
        text = Regex.Replace(text, @"[#*`>\[\]()!_-]", " ");
        text = Regex.Replace(text, @"\s+", " ").Trim();

        return text.Length <= maxLength ? text : text[..maxLength].TrimEnd() + "…";
    }

    public string StripMarkdown(string markdown)
    {
        return Excerpt(markdown, int.MaxValue);
    }

    /// <summary>Simple tag list parser: "dotnet, performance, ai" → ["dotnet","performance","ai"].</summary>
    public List<string> ParseTags(string? tagsInput)
    {
        if (string.IsNullOrWhiteSpace(tagsInput))
            return new List<string>();

        return tagsInput.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();
    }
}

/// <summary>Minimal paging metadata shared by list views.</summary>
public class PagedInfo
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalCount { get; set; }
    public int TotalPages => PageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

/// <summary>Minimal paged result used by list views.</summary>
public class PagedResult<T> : PagedInfo
{
    public PagedResult(IReadOnlyList<T> items, int page, int pageSize, int totalCount)
    {
        Items = items;
        Page = page;
        PageSize = pageSize;
        TotalCount = totalCount;
    }

    public IReadOnlyList<T> Items { get; }
}
