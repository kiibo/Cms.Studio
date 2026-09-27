using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;

namespace Cms.Studio.Core.Services;

/// <summary>Markdown rendering, slug generation and small text helpers.</summary>
public class ContentService
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseAutoIdentifiers(AutoIdentifierOptions.GitHub)
        .Build();

    private static readonly Regex Heading = new("<h(?<level>[23])[^>]*id=\"(?<id>[^\"]+)\"[^>]*>(?<text>.*?)</h[23]>", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex StandaloneImage = new(@"<p>(<img\b[^>]*/?>)</p>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ImgAlt = new("alt=\"(?<alt>[^\"]*)\"", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string ToHtml(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return string.Empty;

        var html = Markdown.ToHtml(markdown, Pipeline);
        return FigureImages(html);
    }

    /// <summary>
    /// Wraps standalone images in &lt;figure&gt; with a caption from the alt text —
    /// the "图文并茂" look: image blocks with captions instead of bare inline images.
    /// </summary>
    public string FigureImages(string html)
    {
        return StandaloneImage.Replace(html, m =>
        {
            var img = m.Groups[1].Value;
            var alt = ImgAlt.Match(img);
            var caption = alt.Success ? alt.Groups["alt"].Value.Trim() : string.Empty;
            return caption.Length == 0
                ? $"<figure class=\"content-figure\">{img}</figure>"
                : $"<figure class=\"content-figure\">{img}<figcaption>{caption}</figcaption></figure>";
        });
    }

    /// <summary>Builds a table of contents from the rendered HTML headings (for the side navigation).</summary>
    public List<TocItem> ExtractToc(string html)
    {
        var toc = new List<TocItem>();
        foreach (Match m in Heading.Matches(html))
        {
            var text = Regex.Replace(m.Groups["text"].Value, "<[^>]+>", string.Empty).Trim();
            if (text.Length == 0)
                continue;
            toc.Add(new TocItem
            {
                Level = int.Parse(m.Groups["level"].Value),
                Text = text,
                Anchor = m.Groups["id"].Value
            });
        }
        return toc;
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

/// <summary>One entry of the article table of contents.</summary>
public class TocItem
{
    /// <summary>2 for H2, 3 for H3.</summary>
    public int Level { get; set; }
    public string Text { get; set; } = string.Empty;
    /// <summary>Heading anchor id.</summary>
    public string Anchor { get; set; } = string.Empty;
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
