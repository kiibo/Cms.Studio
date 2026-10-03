using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
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
        ReconcileSchema(db);

        if (db.Posts.Any())
            return;

        var now = DateTime.UtcNow;

        db.Settings.AddRange(
            new Setting { Key = "SiteTitle", Value = "Cms.Studio" },
            new Setting { Key = "SiteDescription", Value = "Real-time tech news on smartphones, computers and smart devices — written for humans and machines." },
            new Setting { Key = "SiteBaseUrl", Value = "https://example.com" },
            new Setting { Key = "PostsPerPage", Value = "10" },
            new Setting { Key = "AuthorName", Value = "Admin" });

        var phones = new Category { Name = "Smartphones", Slug = "smartphones", Description = "Phones, chips, cameras and everything pocket-sized.", ShowOnMenu = true, DisplayOrder = 1 };
        var computers = new Category { Name = "Computers", Slug = "computers", Description = "Laptops, desktops, GPUs and PC hardware.", ShowOnMenu = true, DisplayOrder = 2 };
        var smart = new Category { Name = "Smart Devices", Slug = "smart-devices", Description = "Wearables, smart home, audio and connected gadgets.", ShowOnMenu = true, DisplayOrder = 3 };
        db.Categories.AddRange(phones, computers, smart);

        var series = new Series { Title = "Fall Launch Season", Slug = "fall-launch-season", Description = "Coverage of the autumn hardware launches." };
        db.Series.Add(series);
        db.SaveChanges();

        var p1 = new Post
        {
            Slug = "flagship-phone-camera-showdown",
            Title = "Flagship Phone Camera Showdown: Night Mode Gets Real",
            Summary = "We compared the latest flagship phone cameras after dark — bigger sensors, smarter processing, and the gap between them is finally shrinking.",
            ContentMd = """
                # Flagship Phone Camera Showdown: Night Mode Gets Real

                This year's flagship phones all promise "DSLR-like" night shots. We took them outside after dark
                to see who actually delivers.

                ## What changed this generation

                - **Bigger sensors.** The main cameras now sit at 1/1.3" or larger, letting in noticeably more light.
                - **Smarter processing.** Multi-frame stacking happens in under a second — no more tripod wobble.
                - **Periscope zoom.** 5x optical is the new normal at the top end.

                ## The verdict

                Daylight remains a coin flip, but in true low light the lead is now decided by processing
                philosophy: one camp keeps it bright and clean, the other keeps it moody and realistic.

                ```text
                Night mode score (out of 100): 92 / 90 / 88 / 85
                ```

                Pick the look you prefer — the hardware race has quietly become a taste question.
                """,
            AuthorName = "Admin",
            CategoryId = phones.Id,
            CoverImageUrl = "https://picsum.photos/seed/tech-phone/960/540",
            Status = PostStatus.Published,
            IsFixedTop = true,
            PublishedOnUtc = now,
            CreatedOnUtc = now
        };
        var p2 = new Post
        {
            Slug = "arm-laptops-battery-king",
            Title = "ARM Laptops Are the New Battery Kings",
            Summary = "The latest ARM-based laptops push past 20 hours of real-world battery life while finally keeping legacy apps happy.",
            ContentMd = """
                # ARM Laptops Are the New Battery Kings

                The newest generation of ARM laptops has crossed a psychological line: you can leave the charger
                at home for a full work trip.

                ## By the numbers

                | Workload | Battery life |
                |---|---|
                | Video playback | 22 h |
                | Web + docs | 16 h |
                | Compile-heavy dev | 9 h |

                ## Compatibility is no longer the catch

                Translation layers now run almost everything at native speed, and the handful of legacy tools
                that still misbehave are increasingly rare.

                > Bottom line: if battery life tops your list, ARM is the safe pick today.
                """,
            AuthorName = "Admin",
            CategoryId = computers.Id,
            CoverImageUrl = "https://picsum.photos/seed/tech-laptop/960/540",
            Status = PostStatus.Published,
            PublishedOnUtc = now.AddHours(-3),
            CreatedOnUtc = now.AddHours(-3)
        };
        var p3 = new Post
        {
            Slug = "smart-ring-wearables-2026",
            Title = "Smart Rings Are Having Their Moment",
            Summary = "Sleep tracking without a screen on your wrist: the smart ring category is growing up, and the data is getting genuinely useful.",
            ContentMd = """
                # Smart Rings Are Having Their Moment

                Screenless wearables had a quiet year — then the smart ring category exploded.

                ## Why a ring works

                1. **Sleep first.** No glowing screen waking you (or your partner) at 2 a.m.
                2. **Weeks of battery.** Tiny cell, tiny draw — most rings last 5–7 days.
                3. **Invisible data.** Recovery and readiness scores without notification fatigue.

                ![Smart ring on a desk](https://picsum.photos/seed/tech-ring/960/540)

                The trade-off remains the same: no screen means you check your phone for everything.
                """,
            AuthorName = "Admin",
            CategoryId = smart.Id,
            CoverImageUrl = "https://picsum.photos/seed/tech-ring/960/540",
            Status = PostStatus.Published,
            PublishedOnUtc = now.AddHours(-6),
            CreatedOnUtc = now.AddHours(-6)
        };
        db.Posts.AddRange(p1, p2, p3);

        var p4 = new Post
        {
            Slug = "usb-c-everything",
            Title = "USB-C Everything: The Charger Drawer Finally Unifies",
            Summary = "One cable to rule them all is no longer a meme — here is what actually fast-charges what.",
            ContentMd = """
                # USB-C Everything: The Charger Drawer Finally Unifies

                - Phones, laptops, tablets and earbuds now share one connector
                - The mess moved to labels: PD, PPS, Watts — here is the cheat sheet
                - A 65W GaN brick covers almost every device in a travel bag
                """,
            AuthorName = "Admin",
            CategoryId = smart.Id,
            Status = PostStatus.Published,
            PublishedOnUtc = now.AddHours(-9),
            CreatedOnUtc = now.AddHours(-9)
        };
        var p5 = new Post
        {
            Slug = "gpus-get-affordable",
            Title = "Mid-Range GPUs Get Interesting Again",
            Summary = "Competition is back in the mid-range: better rasterisation, better efficiency, and prices that make sense for 1440p gaming.",
            ContentMd = """
                # Mid-Range GPUs Get Interesting Again

                After two noisy generations, the middle of the stack is where the interesting fight is happening.

                - 1440p high-refresh is now the default target
                - Power draw is down double digits across the board
                - Upscaling quality finally survives motion

                ![GPU close-up](https://picsum.photos/seed/tech-gpu/960/540)
                """,
            AuthorName = "Admin",
            CategoryId = computers.Id,
            CoverImageUrl = "https://picsum.photos/seed/tech-gpu/960/540",
            Status = PostStatus.Published,
            PublishedOnUtc = now.AddHours(-12),
            CreatedOnUtc = now.AddHours(-12)
        };
        db.Posts.AddRange(p4, p5);
        db.PostSeries.Add(new PostSeries { Post = p1, Series = series, DisplayOrder = 1 });
        db.PostSeries.Add(new PostSeries { Post = p2, Series = series, DisplayOrder = 2 });

        var tagPhones = new Tag { Name = "Phones", Slug = "phones" };
        var tagLaptops = new Tag { Name = "Laptops", Slug = "laptops" };
        var tagWearables = new Tag { Name = "Wearables", Slug = "wearables" };
        var tagGpus = new Tag { Name = "GPUs", Slug = "gpus" };
        db.Tags.AddRange(tagPhones, tagLaptops, tagWearables, tagGpus);
        db.SaveChanges();

        db.PostTags.AddRange(
            new PostTag { PostId = p1.Id, TagId = tagPhones.Id },
            new PostTag { PostId = p2.Id, TagId = tagLaptops.Id },
            new PostTag { PostId = p3.Id, TagId = tagWearables.Id },
            new PostTag { PostId = p5.Id, TagId = tagGpus.Id });

        db.PostFeedback.AddRange(
            new PostFeedback { PostId = p1.Id, Ip = "203.0.113.10", IsValuable = true, CreatedOnUtc = now, UpdatedOnUtc = now },
            new PostFeedback { PostId = p1.Id, Ip = "203.0.113.11", IsValuable = true, CreatedOnUtc = now, UpdatedOnUtc = now },
            new PostFeedback { PostId = p1.Id, Ip = "203.0.113.12", IsValuable = false, CreatedOnUtc = now, UpdatedOnUtc = now },
            new PostFeedback { PostId = p2.Id, Ip = "203.0.113.10", IsValuable = true, CreatedOnUtc = now, UpdatedOnUtc = now },
            new PostFeedback { PostId = p2.Id, Ip = "203.0.113.13", IsValuable = true, CreatedOnUtc = now, UpdatedOnUtc = now });

        var c1 = new Comment
        {
            PostId = p1.Id,
            NickName = "Alice",
            Email = "alice@example.com",
            Content = "Great comparison — the night mode samples really show how different the processing philosophies are.",
            CreatedOnUtc = now,
            IsApproved = true
        };
        db.Comments.Add(c1);
        db.SaveChanges();

        db.Comments.Add(new Comment
        {
            PostId = p1.Id,
            ParentId = c1.Id,
            NickName = "Admin",
            Email = "admin@example.com",
            Content = "Thanks! We will follow up with a video version of the same scene once the next update lands.",
            CreatedOnUtc = now.AddMinutes(20),
            IsApproved = true
        });
        db.Comments.Add(new Comment
        {
            PostId = p2.Id,
            NickName = "Bob",
            Email = "bob@example.com",
            Content = "20+ hours of real battery is the point where I stop caring about carrying a charger. Count me in.",
            CreatedOnUtc = now.AddHours(-1),
            IsApproved = false
        });

        db.SaveChanges();
    }

    // ---- schema reconciliation -------------------------------------------------
    // EnsureCreated() only builds a database that does not exist yet; it never alters
    // an existing one. A local file created by an older build can therefore be missing
    // newer tables / columns, which surfaces later as "no such table" / "no such column"
    // (e.g. when the admin dashboard queries the visitor-analytics tables). Reconcile the
    // schema non-destructively: create whatever is missing, never drop or rewrite data.
    private static void ReconcileSchema(CmsDbContext db)
    {
        try
        {
            EnsureMissingTables(db);
            EnsureMissingColumns(db);
        }
        catch
        {
            // Introspection / DDL helpers can be unavailable for some providers. The
            // fresh-database path is already handled by EnsureCreated() above, so an
            // unusual provider must never block startup here.
        }
    }

    private static void EnsureMissingTables(CmsDbContext db)
    {
        var creator = db.GetInfrastructure().GetRequiredService<IRelationalDatabaseCreator>();
        var script = creator.GenerateCreateScript();
        foreach (var raw in script.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var stmt = MakeIdempotent(raw);
            if (stmt is null) continue;
            try
            {
                db.Database.ExecuteSqlRaw(stmt);
            }
            catch
            {
                // One statement (e.g. an index on a column not yet added) may fail; keep
                // going so the remaining missing objects are still created.
            }
        }
    }

    /// <summary>Turns a generated CREATE statement into a CREATE ... IF NOT EXISTS one,
    /// or returns null for anything that is not a table / index creation.</summary>
    private static string? MakeIdempotent(string raw)
    {
        // Drop leading blank / comment lines that can follow a previous statement.
        var lines = raw.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith("--"))
            .ToArray();
        var s = string.Join(" ", lines).Trim();
        if (s.Length == 0) return null;

        foreach (var kw in new[] { "CREATE TABLE ", "CREATE UNIQUE INDEX ", "CREATE INDEX " })
        {
            if (s.StartsWith(kw, StringComparison.OrdinalIgnoreCase))
                return s.Substring(0, kw.Length) + "IF NOT EXISTS " + s.Substring(kw.Length);
        }
        return null;
    }

    private static void EnsureMissingColumns(CmsDbContext db)
    {
        foreach (var entity in db.Model.GetEntityTypes())
        {
            var table = entity.GetTableName();
            if (string.IsNullOrEmpty(table)) continue;
            var storeObj = StoreObjectIdentifier.Table(table, entity.GetSchema());

            var existing = ReadExistingColumns(db, table!);
            foreach (var prop in entity.GetProperties())
            {
                var column = prop.GetColumnName(storeObj);
                if (string.IsNullOrEmpty(column) || existing.Contains(column!)) continue;

                var colType = prop.GetColumnType() ?? "TEXT";
                var ddl = $"ALTER TABLE \"{Escape(table!)}\" ADD COLUMN \"{Escape(column!)}\" {colType}";
                if (!prop.IsColumnNullable(storeObj))
                    ddl += $" NOT NULL DEFAULT {DefaultLiteral(prop)}";
                try
                {
                    db.Database.ExecuteSqlRaw(ddl);
                }
                catch
                {
                    // Adding an individual column can fail on exotic constraints; skip it.
                }
            }
        }
    }

    private static HashSet<string> ReadExistingColumns(CmsDbContext db, string table)
    {
        var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var conn = db.Database.GetDbConnection();
        var wasClosed = conn.State != System.Data.ConnectionState.Open;
        if (wasClosed) conn.Open();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info(\"{Escape(table)}\")";
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
                cols.Add(rdr.GetString(rdr.GetOrdinal("name")));
        }
        finally
        {
            if (wasClosed) conn.Close();
        }
        return cols;
    }

    private static string DefaultLiteral(IProperty prop)
    {
        var t = prop.ClrType;
        if (t == typeof(string)) return "''";
        if (t == typeof(DateTime) || t == typeof(DateTimeOffset)) return "''";
        return "0"; // bool + numerics
    }

    private static string Escape(string id) => id.Replace("\"", "\"\"");
}
