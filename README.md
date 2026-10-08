# Cms.Studio (.NET 10)

A standalone, lightweight English blog / CMS built for **SEO and AI (ChatGPT, etc.) ingestion**.
Combines nopCommerce's multi-database & admin management strengths with Masuit.MyBlogs' clean blogging features — without the e-commerce core.

## Features

- **Writing**: Vditor Markdown editor (self-hosted, no CDN) with toolbar, split / WYSIWYG / preview modes,
  outline, fullscreen and **drag & drop / paste image upload**; draft/published status, pin to top, summary, cover image
- **Homepage images**: hero section (1 large + 2 small featured posts) and per-post thumbnails;
  the cover is the post's cover URL, falling back to the first image in the Markdown body (masuit.blog-style)
- **Comments** (masuit.blog-style): guests leave a nick name, email and comment; a 6-digit **verification code**
  is e-mailed to the address and must be submitted with the comment. Codes are valid 24h, single use, re-send
  throttled to 2 min. Comments land as **pending** and are published from the admin area after approval.
  Threaded one level (reply). Duplicate comments are rejected within a 5-minute window.
- **Multi-database** (nopCommerce-style provider selection): SQL Server / MySQL / PostgreSQL / SQLite, switched in `appsettings.json`
- **SEO**: clean slug URLs, canonical, Open Graph / Twitter Card, JSON-LD `BlogPosting`, `sitemap.xml`, `robots.txt`, RSS, automatic 301 on slug change
- **AI ingestion**: `llms.txt` (structured site index), `llms-full.txt` (full-text Markdown mirror), raw Markdown at `/blog/{slug}.md`
- **Rankings**: Daily / Weekly / Monthly top lists (from per-day view stats); mobile entries live in the navigation bar
- **Visitor analytics**: every page request is recorded (IP, User-Agent, device / browser / OS, path, referrer);
  the admin dashboard shows a 30-day visitor curve where **the same IP on the same day counts once**,
  plus PV/UV counters, device/browser/OS distributions and top IPs
- **Admin**: dashboard (stat cards + 30-day trend + monthly Top 10), posts / categories / series / tags / settings

## Quick start

```bash
cd Cms.Studio   # repository root
dotnet run --project Cms.Studio.Web
```

The database is created and seeded with sample data on first run.
Admin area: `/admin/login` (default `admin` / `change-me` — change it in appsettings.json).

## Comment verification e-mail

`appsettings.json` → `Smtp` (Host / Port / UserName / Password / From / EnableSsl).
With `Host` left empty and the `Development` environment, the verification code is shown directly on the
page instead of being e-mailed — handy for local testing. In production, configure SMTP so codes are delivered.

Comment moderation: `/admin/comments` (Pending / All, Approve / Delete).

## Database configuration

`Cms.Studio.Web/appsettings.json`:

```jsonc
"DataProvider": {
  "ProviderName": "MySql",                         // SqlServer | MySql | PostgreSQL | Sqlite
  "ConnectionString": "Server=localhost;Port=3306;Database=cmsstudio;User Id=root;Password=your_password;CharSet=utf8mb4",
  "CharacterSet": "utf8mb4",                       // used when the database is auto-created
  "Collation": "utf8mb4_unicode_ci"
}
```

**MySQL is the production default.** Startup runs a nopCommerce-style bootstrap
(`DatabaseBootstrapper` / `DbInitializer`):

1. **Creates the database** named in the connection string when it does not exist yet —
   `CREATE DATABASE IF NOT EXISTS ... CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci`
   (charset / collation from `CharacterSet` / `Collation`) — and waits until the fresh
   database accepts connections before anything else runs.
2. **Writes the table structure** with `EnsureCreated()`, then non-destructively reconciles
   whatever an older build may be missing (extra tables / columns are added, nothing is
   dropped or rewritten).
3. **Seeds** default settings and sample content on the very first run.

The configured account needs the `CREATE` privilege once; afterwards the app runs with
normal DML rights. A pre-created database works too — just point the connection string at
it. SQLite stays available for zero-config local development
(`"ProviderName": "Sqlite"` / `"ConnectionString": "Data Source=cmsstudio.db"`), and
SQL Server / PostgreSQL get the same auto-create treatment via their "master" / "postgres"
maintenance connections.

## URL map

| URL | Description |
|---|---|
| `/` | Home feed |
| `/blog/{slug}` | Post page (JSON-LD, canonical, OG) |
| `/blog/{slug}.md` | Raw post Markdown (AI friendly) |
| `/category/{slug}` `/tag/{slug}` `/series/{slug}` | Category / tag / series listings |
| `/archive/{year}/{month}` | Monthly archive |
| `/rank/daily` `/rank/weekly` `/rank/monthly` | Daily / Weekly / Monthly rankings |
| `/search?q=` | Search |
| `/rss` `/sitemap.xml` `/robots.txt` | Subscriptions & SEO |
| `/llms.txt` `/llms-full.txt` | LLM site index / full-text mirror |
| `/admin` | Admin area (posts, **comments moderation**, categories, series, tags, settings) |

## Project structure

```
Cms.Studio.Core   Entities / multi-database DbContext / services (posts, categories, tags, series, stats, settings, Markdown)
Cms.Studio.Web    MVC controllers + Razor views (public + admin, all in English)
```

## Verified

- `dotnet build` passes warning-free (`.NET 10`)
- Smoke tests: all public endpoints, all admin endpoints, the sign-in flow, and the full comment flow
  (send code → verify → pending → approve) pass
- **MySQL 8.0 end-to-end**: pointing `appsettings.json` at a running MySQL server where `cmsstudio`
  does **not** exist yet → the database is auto-created with `utf8mb4` / `utf8mb4_unicode_ci`, all 12
  tables and the sample seed are written on first run, and the site answers 200 immediately.
  Also verified: an empty pre-created database receives the full schema, a dropped table / column is
  healed on the next start without re-seeding, and both `mysql_native_password` and
  `caching_sha2_password` accounts work (as does SQLite).

> Schema note: the schema is created with `EnsureCreated` + startup reconciliation, so newer tables /
> columns are added automatically. For SQLite, deleting `Cms.Studio.Web/cmsstudio.db` still resets the
> demo database to fresh sample data.

## Environment note

In environments with a low `RLIMIT_FSIZE` (e.g. some sandboxes), the .NET runtime's W^X feature trips
SIGXFSZ on its 2 TiB JIT memory file. Set `DOTNET_EnableWriteXorExecute=0` to run normally there.

## Editions

This repository ships the **.NET 10 edition** (`net10.0`, EF Core 10, admin UI in English) only.
Older experimental editions are not part of this repo.

## Deployment behind nginx (real visitor IPs)

Reverse proxies hide the client IP: Kestrel only sees the proxy (e.g. `127.0.0.1`). The app
ships with ForwardedHeaders configured to trust **IPv4/IPv6 loopback + private networks**,
so a local nginx just needs to pass the standard headers:

```nginx
location / {
    proxy_pass http://127.0.0.1:1001;
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
}
```

With these headers, `Connection.RemoteIpAddress` (visitor stats, comment IPs, …) carries the
real client address. If your proxy lives outside those ranges, add its address to
`ForwardedHeadersOptions.KnownProxies` in `Program.cs`. Directly-exposed instances ignore
forged `X-Forwarded-For` headers (untrusted sources are never accepted).
