# Comments SPA — Middle Level Test Task

A full-stack SPA for threaded comments, built with **.NET 9 + Angular + PostgreSQL** and extended with Middle-level integrations.

## Stack

| Layer | Technology |
|-------|-----------|
| Backend | .NET 9, ASP.NET Core, EF Core 9 |
| Frontend | Angular 20 (standalone components) |
| Relational DB | PostgreSQL 17 |
| Cache | Redis 7 |
| Message broker | RabbitMQ 3 |
| Search | Elasticsearch 8 |
| Real-time | SignalR (WebSocket) |
| GraphQL | HotChocolate 15 |
| Containerisation | Docker + docker-compose |
| Cloud target | Azure (App Service / Container Apps + Azure Database for PostgreSQL + Azure Cache for Redis) |

## Features

### Core
- Threaded comment tree (infinite nesting)
- Fields: User Name (alphanumeric), E-mail, Home page (optional URL), CAPTCHA, Text
- Server-side and client-side validation
- XSS protection — only `<i>`, `<strong>`, `<code>`, `<a href="…">` tags allowed; XHTML nesting validated
- Sorting by User Name / E-mail / Date, ascending and descending (default LIFO)
- Pagination (25 comments per page)
- SQL injection prevention via parameterised EF Core queries

### Attachments
- Image upload (JPG, GIF, PNG) — auto-resized to max 320×240 px
- Text file upload (TXT) — max 100 KB
- Lightbox viewer for images with zoom animation
- In-page viewer (modal) for text files
- Remove attachment button before submitting

### Markup toolbar
- Buttons for `[i]`, `[strong]`, `[code]`, `[a]` — wraps selected text

### Message preview
- Live preview without page reload (AJAX POST `/api/comments/preview`)

### Middle-level integrations
- **RabbitMQ** — publishes `comments.created` event to durable queue on every new comment; `CommentCreatedConsumer` background service processes messages
- **Elasticsearch** — indexes every comment via RabbitMQ consumer; full-text search across `userName`, `email`, `text` via `GET /api/comments/search?q=…`
- **Redis** — CAPTCHA challenge cache + cache-aside pattern for top-level comment count (`comments:toplevel:total`)
- **SignalR / WebSocket** — new comments pushed to all connected clients in real time; live indicator in UI
- **GraphQL** — `Query`, `Mutation` (createComment), `Subscription` (onCommentCreated) at `/graphql` (HotChocolate 15)
- **Scalar** — interactive API documentation at `/scalar/v1`

## Quick start with Docker

```bash
git clone <repo-url>
cd testtask_sidak_middle
docker compose up --build
```

| Service | URL |
|---------|-----|
| Frontend | http://localhost:4200 |
| API | http://localhost:8080 |
| API docs (Scalar) | http://localhost:8080/scalar/v1 |
| GraphQL playground | http://localhost:8080/graphql |
| RabbitMQ management | http://localhost:15672 (guest/guest) |
| Elasticsearch | http://localhost:9200 |

> First startup may take ~60 s while Elasticsearch initialises.

## Local backend development

```bash
# Requires: .NET 9 SDK, PostgreSQL, Redis, RabbitMQ, Elasticsearch running locally
cd CommentsApp.Api
dotnet restore
dotnet run
```

Configuration overrides for local dev are in `appsettings.Development.json`.

## API endpoints

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/captcha/new` | Generate CAPTCHA challenge |
| `GET` | `/api/comments` | List top-level comments (paginated, sorted) |
| `POST` | `/api/comments` | Create comment (multipart/form-data) |
| `POST` | `/api/comments/preview` | Preview sanitised HTML |
| `GET` | `/api/comments/search?q=…` | Full-text search via Elasticsearch |
| `GET` | `/api/attachments/{filename}` | Download attachment |
| `GET` | `/graphql` | GraphQL endpoint |
| WS | `/hubs/comments` | SignalR hub |

## Database schema

See [`db-schema.sql`](db-schema.sql) — compatible with MySQL Workbench (standard SQL DDL).

EF Core migrations are in [`CommentsApp.Api/Migrations/`](CommentsApp.Api/Migrations/) and are applied automatically on startup.

## Azure deployment

| Component | Azure service |
|-----------|--------------|
| API | Azure Container Apps or App Service |
| Frontend | Azure Static Web Apps |
| PostgreSQL | Azure Database for PostgreSQL – Flexible Server |
| Redis | Azure Cache for Redis |
| RabbitMQ | Azure Container Apps (sidecar) or CloudAMQP |
| Elasticsearch | Elastic Cloud on Azure |
| Container registry | Azure Container Registry |

Set environment variables matching the keys in `appsettings.json` via App Service Configuration or Container Apps secrets.

## Self-check

```bash
# 1. Clone fresh
git clone <repo-url> test-run && cd test-run
# 2. Start everything
docker compose up --build
# 3. Open http://localhost:4200 and:
#    - Post a comment with CAPTCHA, attachment
#    - Verify it appears in real time (Live indicator)
#    - Search via Elasticsearch
#    - Check RabbitMQ queue at http://localhost:15672
```
