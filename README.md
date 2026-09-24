# Cms.Studio (.NET 10)

A standalone, lightweight English blog / CMS built for **SEO and AI (ChatGPT, etc.) ingestion**.
Combines nopCommerce's multi-database & admin management strengths with Masuit.MyBlogs' clean blogging features — without the e-commerce core.

## Features

- **Writing**: web-based Markdown editor, draft/published status, pin to top, summary, cover image
- **Comments** (masuit.blog-style): guests leave a nick name, email and comment; a 6-digit **verification code**
  is e-mailed to the address and must be submitted with the comment. Codes are valid 24h, single use, re-send
  throttled to 2 min. Comments land as **pending** and are published from the admin area after approval.
  Threaded one level (reply). Duplicate comments are rejected within a 5-minute window.
- **Multi-database** (nopCommerce-style provider selection): SQL Server / MySQL / PostgreSQL / SQLite, switched in `appsettings.json`
- **SEO**: clean slug URLs, canonical, Open Graph / Twitter Card, JSON-LD `BlogPosting`, `sitemap.xml`, `robots.txt`, RSS, automatic 301 on slug change
- **AI ingestion**: `llms.txt` (structured site index), `llms-full.txt` (full-text Markdown mirror), raw Markdown at `/blog/{slug}.md`
- **Rankings**: Daily / Weekly / Monthly top lists (from per-day view stats); mobile entries live in the navigation bar
- **Admin**: dashboard (stat cards + 30-day trend + monthly Top 10), posts / categories / series / tags / settings

## Quick start

```bash
cd src/Cms.Studio.Net10
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
  "ProviderName": "Sqlite",                       // SqlServer | MySql | PostgreSQL | Sqlite
  "ConnectionString": "Data Source=cmsstudio.db"
}
```

MySQL example:

```jsonc
"DataProvider": {
  "ProviderName": "MySql",
  "ConnectionString": "Server=localhost;Database=cmsstudio;User=root;Password=***;AllowUserVariables=true"
}
```

The .NET 10 edition uses Oracle's `MySql.EntityFrameworkCore` provider (10.x) — Pomelo has not shipped
an EF Core 10 release yet.

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

- `dotnet build` passes (`.NET 10`)
- Smoke tests: all public endpoints, all admin endpoints, the sign-in flow, and the full comment flow
  (send code → verify → pending → approve) pass

> Schema note: the demo database is created with `EnsureCreated`. After pulling a version with new tables,
> delete `Cms.Studio.Web/cmsstudio.db` and it will be recreated with the new schema and fresh sample data.

## Environment note

In environments with a low `RLIMIT_FSIZE` (e.g. some sandboxes), the .NET runtime's W^X feature trips
SIGXFSZ on its 2 TiB JIT memory file. Set `DOTNET_EnableWriteXorExecute=0` to run normally there.

## Difference from the net9 version

`src/Cms.Studio/` is the original .NET 9 edition (admin UI in Chinese). This .NET 10 edition targets
`net10.0` with EF Core 10 and ships with the whole UI in English. Both are independent and can run side by side.
