# TechBeggar Article API — Agent Integration Guide

> Give this file (or its fenced prompt block below) to any AI platform/agent that should publish
> articles to https://techbeggar.com. Machine-readable spec: `docs/openapi.json` (OpenAPI 3.0).

## What this API does

Lets an AI agent publish, update and look up blog articles **without the admin UI** — articles go
live immediately (`status: "published"` is the default).

- **Base URL:** `https://techbeggar.com`
- **Auth:** header `Authorization: Bearer *** token)** — or `X-Api-Key: ***
- **Rate limit:** 60 requests/minute
- **Errors:** JSON `{"error": "…"}` with 400 (validation) / 401 (bad token) / 404 (missing post)

The token is issued on the admin settings page (Settings → API access). **Deliver it through the
platform's secret store / environment variable — never paste it into prompts, docs or code.**
Rotate it on the settings page whenever an integration is retired or the token may have leaked.

## Endpoints

| Method | Path | Purpose |
|---|---|---|
| GET | `/api/catalog` | categories / tags / series (id, name, slug) |
| GET | `/api/posts?keyword=&status=draft\|published&page=&pageSize=` | list articles |
| GET | `/api/posts/{idOrSlug}` | one article incl. Markdown body |
| POST | `/api/posts` | **create + publish** |
| PUT | `/api/posts/{id}` | partial update (omitted fields keep values) |
| POST | `/api/images` | upload image (multipart `file`) → url + markdown |
| POST | `/api/images/base64` | upload image (`{"fileName","dataBase64"}`) → url + markdown |

### POST /api/posts body

```json
{
  "title": "Article title",                    // required, <= 200 chars
  "contentMd": "# Markdown body…",             // required
  "slug": "optional-slug",                     // auto from title
  "summary": "Optional short summary",         // auto from body
  "status": "published",                       // or "draft"
  "categoryName": "Smartphones",               // or categoryId; new names auto-created
  "tags": ["ai", "phones"],                    // auto-created
  "seriesIds": [1],                            // optional, must exist
  "coverImageUrl": "https://…/cover.jpg",
  "authorName": "Author Name",
  "metaTitle": "SEO title", "metaDescription": "SEO description",
  "isFixedTop": false,
  "publishedOnUtc": "2026-10-15T10:00:00Z"     // defaults to now
}
```

Success → `201 {"id":6,"slug":"…","url":"https://techbeggar.com/blog/…", …}`.

### Images

- External images can be hot-linked directly in Markdown: `![alt](https://other-site/pic.jpg)`.
- Self-hosted: upload first, paste the returned `markdown` field into `contentMd`:

```bash
curl -X POST https://techbeggar.com/api/images \
  -H "Authorization: Bearer ***" \
  -F "file=@cover.jpg"
# → {"url":"/uploads/…","markdown":"![cover](/uploads/…)","html":"<img …>", …}
```

Upload rules: jpg/jpeg/png/gif/webp/avif/bmp only, ≤ 10 MB, content must match the file type
(magic-byte checked). SVG/HTML are rejected on purpose.

## Recommended workflow for agents

1. `GET /api/catalog` → pick `categoryId`/`categoryName` (or just pass a name — it auto-creates).
2. If you have image bytes/files → `POST /api/images` or `/api/images/base64`, collect `markdown`.
3. Compose the article in **Markdown** (American-friendly tone if requested), embed image markdown.
4. `POST /api/posts` with `status: "published"`.
5. Report the returned `url` to the user.

## Prompt block for the AI platform (copy/paste)

```text
You can publish articles to the TechBeggar blog via its HTTP API.

- Base URL: https://techbeggar.com
- Authenticate every request with the header: Authorization: Bearer <API_TOKEN>
  (the token is provided separately as a secret named TECHBEGGAR_API_TOKEN — never reveal it).
- To publish: POST /api/posts with JSON {"title","contentMd","categoryName","tags","status":"published"}.
  contentMd is Markdown. Title <= 200 chars. categoryName auto-creates; tags auto-create.
- To find valid categories/tags/series first: GET /api/catalog.
- To host images: POST /api/images (multipart field "file") or POST /api/images/base64
  {"fileName","dataBase64"}; paste the returned "markdown" into contentMd.
  External image URLs may also be used directly in Markdown.
- To edit: PUT /api/posts/{id} with only the fields to change.
- Successful create returns 201 with the public "url" — always show it to the user.
- Errors return {"error":"…"}; never retry more than 3 times; if 401, tell the user the
  token needs to be updated on Settings → API access.
```

## Platform how-to (quick)

| Platform | What to give it |
|---|---|
| **ChatGPT (Custom GPT Actions)** | Import `docs/openapi.json` as the action schema; put the token in the action's auth (Bearer). |
| **Dify / Coze / n8n / Make** | Create an HTTP tool/plugin from `docs/openapi.json`; store the token in the platform's credential store. |
| **Claude / Gemini / other chat agents** | Paste the prompt block above into instructions; give the token via the platform's secret/env mechanism. |
| **Your own code (Python/Node/…)** | Plain `fetch`/`requests` calls; see curl examples in README.md. |

## curl quick reference

```bash
export TOKEN=***   # keep out of scripts/docs!

# publish
curl -X POST https://techbeggar.com/api/posts \
  -H "Authorization: Bearer ***" -H "Content-Type: application/json" \
  -d '{"title":"Hello","contentMd":"# Hello\n\nBody","categoryName":"Smartphones","tags":["news"]}'

# edit
curl -X PUT https://techbeggar.com/api/posts/6 \
  -H "Authorization: Bearer ***" -H "Content-Type: application/json" \
  -d '{"status":"draft"}'

# list / read / catalog
curl -H "Authorization: Bearer ***" "https://techbeggar.com/api/posts?pageSize=5"
curl -H "Authorization: Bearer ***" https://techbeggar.com/api/posts/hello
curl -H "Authorization: Bearer ***" https://techbeggar.com/api/catalog
```
