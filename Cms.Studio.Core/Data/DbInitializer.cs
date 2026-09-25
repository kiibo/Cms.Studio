using Microsoft.Extensions.DependencyInjection;
using Cms.Studio.Core.Domain;

namespace Cms.Studio.Core.Data;

/// <summary>Creates the schema on first run and seeds default settings + sample content.</summary>
public static class DbInitializer
{
    public static void Initialize(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();

        db.Database.EnsureCreated();

        if (db.Posts.Any())
            return;

        var now = DateTime.UtcNow;

        db.Settings.AddRange(
            new Setting { Key = "SiteTitle", Value = "Cms.Studio" },
            new Setting { Key = "SiteDescription", Value = "Notes on software, AI and technology — written for humans and machines." },
            new Setting { Key = "SiteBaseUrl", Value = "https://example.com" },
            new Setting { Key = "PostsPerPage", Value = "10" },
            new Setting { Key = "AuthorName", Value = "Admin" });

        var ai = new Category { Name = "AI", Slug = "ai", Description = "Artificial intelligence, LLMs and tooling.", ShowOnMenu = true, DisplayOrder = 1 };
        var eng = new Category { Name = "Engineering", Slug = "engineering", Description = "Software engineering, architecture and performance.", ShowOnMenu = true, DisplayOrder = 2 };
        db.Categories.AddRange(ai, eng);

        var series = new Series { Title = "Getting Started", Slug = "getting-started", Description = "A short tour of this site." };
        db.Series.Add(series);
        db.SaveChanges();

        var welcome = new Post
        {
            Slug = "hello-world",
            Title = "Hello, World",
            Summary = "Welcome to Cms.Studio — a lightweight, AI-friendly publishing platform. This sample post shows how articles look and how they are exposed to search engines and LLM crawlers.",
            ContentMd = """
                # Hello, World

                Welcome to **Cms.Studio**. This site is built for writing in English and getting discovered — both by search engines and by AI assistants like ChatGPT.

                ## What makes it AI-friendly?

                - Every article has a clean canonical URL: `/blog/hello-world`
                - Raw Markdown is served at `/blog/hello-world.md`
                - The whole site is summarised in [`llms.txt`](/llms.txt) and fully mirrored in [`llms-full.txt`](/llms-full.txt)
                - Posts are structured with schema.org `BlogPosting` JSON-LD
                - A standard [`sitemap.xml`](/sitemap.xml) and [RSS feed](/rss) are always up to date

                ## Markdown sample

                ```csharp
                var greeting = "Hello from Cms.Studio!";
                Console.WriteLine(greeting);
                ```

                Enjoy writing!
                """,
            AuthorName = "Admin",
            CategoryId = ai.Id,
            CoverImageUrl = "https://picsum.photos/seed/cmsstudio-welcome/960/540",
            Status = PostStatus.Published,
            IsFixedTop = true,
            PublishedOnUtc = now,
            CreatedOnUtc = now
        };
        var writing = new Post
        {
            Slug = "writing-for-humans-and-machines",
            Title = "Writing for Humans and Machines",
            Summary = "A quick guide to structuring English blog posts so that both readers and language models get the most out of them.",
            ContentMd = """
                # Writing for Humans and Machines

                Good technical writing serves two audiences today: human readers and the language models that index the web.

                ## Principles

                1. **Lead with the answer.** Put the key point in the first paragraph.
                2. **Use descriptive headings.** They double as an outline for LLM summarisers.
                3. **Prefer plain English.** Short sentences rank well and parse well.
                4. **Link generously.** Internal links help crawlers; external links help readers.

                > Tip: keep summaries under 160 characters — they become your meta description.
                """,
            AuthorName = "Admin",
            CategoryId = eng.Id,
            CoverImageUrl = "https://picsum.photos/seed/cmsstudio-writing/960/540",
            Status = PostStatus.Published,
            PublishedOnUtc = now.AddHours(-3),
            CreatedOnUtc = now.AddHours(-3)
        };
        var images = new Post
        {
            Slug = "choosing-cover-images",
            Title = "Choosing Cover Images That Work",
            Summary = "Why every post deserves a cover image, how the homepage hero picks its stories, and what makes a thumbnail readable at 168 pixels.",
            ContentMd = """
                # Choosing Cover Images That Work

                The homepage hero and the feed cards pick up the cover image of each post — either the explicit
                cover URL or the first image inside the Markdown body.

                ## Rules of thumb

                1. **Landscape beats portrait.** Cards crop to 3:2.
                2. **Keep the subject centred.** The bottom third is covered by the title gradient.
                3. **High contrast.** White text needs a dark lower edge.

                ![A calm desk setup](https://picsum.photos/seed/cmsstudio-inline/960/540)
                """,
            AuthorName = "Admin",
            CategoryId = eng.Id,
            CoverImageUrl = "https://picsum.photos/seed/cmsstudio-covers/960/540",
            Status = PostStatus.Published,
            PublishedOnUtc = now.AddHours(-6),
            CreatedOnUtc = now.AddHours(-6)
        };
        db.Posts.AddRange(welcome, writing, images);

        var noCoverA = new Post
        {
            Slug = "markdown-tips",
            Title = "Small Markdown Tricks Worth Stealing",
            Summary = "Tables, footnotes, task lists and fenced code — the handful of Markdown features that do most of the heavy lifting in technical posts.",
            ContentMd = """
                # Small Markdown Tricks Worth Stealing

                - Task lists keep tutorials honest: `- [x] done`, `- [ ] todo`
                - Tables beat screenshots for config options
                - Fenced code with a language tag gives you syntax highlighting
                """,
            AuthorName = "Admin",
            CategoryId = eng.Id,
            Status = PostStatus.Published,
            PublishedOnUtc = now.AddHours(-9),
            CreatedOnUtc = now.AddHours(-9)
        };
        var noCoverB = new Post
        {
            Slug = "llms-txt-explained",
            Title = "llms.txt, Explained",
            Summary = "What the llms.txt convention is, why this site serves llms-full.txt and raw Markdown, and how it helps AI assistants quote you correctly.",
            ContentMd = """
                # llms.txt, Explained

                `llms.txt` is a short, structured index of a site written for language models.
                This site also mirrors every post as raw Markdown at `/blog/{slug}.md`.

                ![A simple diagram](https://picsum.photos/seed/cmsstudio-llms/960/540)
                """,
            AuthorName = "Admin",
            CategoryId = ai.Id,
            Status = PostStatus.Published,
            PublishedOnUtc = now.AddHours(-12),
            CreatedOnUtc = now.AddHours(-12)
        };
        db.Posts.AddRange(noCoverA, noCoverB);
        db.PostSeries.Add(new PostSeries { Post = welcome, Series = series, DisplayOrder = 1 });

        var tagAi = new Tag { Name = "AI", Slug = "ai" };
        var tagSeo = new Tag { Name = "SEO", Slug = "seo" };
        var tagWriting = new Tag { Name = "Writing", Slug = "writing" };
        db.Tags.AddRange(tagAi, tagSeo, tagWriting);
        db.SaveChanges();

        db.PostTags.AddRange(
            new PostTag { PostId = welcome.Id, TagId = tagAi.Id },
            new PostTag { PostId = welcome.Id, TagId = tagSeo.Id },
            new PostTag { PostId = writing.Id, TagId = tagWriting.Id },
            new PostTag { PostId = writing.Id, TagId = tagSeo.Id });

        var c1 = new Comment
        {
            PostId = welcome.Id,
            NickName = "Alice",
            Email = "alice@example.com",
            Content = "Great introduction — the llms.txt idea is neat. Looking forward to more posts!",
            CreatedOnUtc = now,
            IsApproved = true
        };
        db.Comments.Add(c1);
        db.SaveChanges();

        db.Comments.Add(new Comment
        {
            PostId = welcome.Id,
            ParentId = c1.Id,
            NickName = "Admin",
            Email = "admin@example.com",
            Content = "Thanks Alice! Full-text Markdown mirrors make the site easy for LLMs to digest.",
            CreatedOnUtc = now.AddMinutes(20),
            IsApproved = true
        });
        db.Comments.Add(new Comment
        {
            PostId = writing.Id,
            NickName = "Bob",
            Email = "bob@example.com",
            Content = "Point 3 is underrated - plain English really does help both readers and models.",
            CreatedOnUtc = now.AddHours(-1),
            IsApproved = false
        });

        db.SaveChanges();
    }
}
