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

Two schema files are included:

| File | Purpose |
|------|---------|
| [`db-schema.sql`](db-schema.sql) | PostgreSQL DDL — open in **pgAdmin**, **DBeaver**, or `psql` to inspect the live schema |
| [`db-schema-mysql-workbench.sql`](db-schema-mysql-workbench.sql) | MySQL Workbench compatible syntax — open in **MySQL Workbench → File → Open Model** to visualise the ER diagram |

> The production database runs on **PostgreSQL 17**. The MySQL Workbench file is provided for diagram visualisation only.

EF Core migrations are in [`CommentsApp.Api/Migrations/`](CommentsApp.Api/Migrations/) and are applied automatically on startup via `db.Database.Migrate()`.

## Azure deployment

The full infrastructure is defined as code in [`azure/main.bicep`](azure/main.bicep) and can be deployed with a single command. CI/CD is configured via GitHub Actions ([`deploy-azure.yml`](.github/workflows/deploy-azure.yml)) — on every push to `main` the images are built, pushed to GHCR, and deployed to Azure Container Apps automatically.

### Architecture

```
                        ┌─────────────────────────────────────┐
                        │           Azure Resource Group        │
                        │                                       │
  User ──► Browser ───►│  Container Apps Env                   │
                        │  ┌──────────┐   ┌──────────────┐    │
                        │  │  Web     │   │     API       │    │
                        │  │ Angular  │──►│  .NET 9       │    │
                        │  │ :4200    │   │  :8080        │    │
                        │  └──────────┘   └──────┬───────┘    │
                        │                         │             │
                        │          ┌──────────────┼──────────┐ │
                        │          │              │          │  │
                        │   ┌──────▼───┐  ┌──────▼───┐      │  │
                        │   │PostgreSQL│  │  Redis   │      │  │
                        │   │Flexible  │  │  Cache   │      │  │
                        │   │Server 17 │  │ Basic C0 │      │  │
                        │   └──────────┘  └──────────┘      │  │
                        │                                    │  │
                        │   ┌──────────┐  ┌──────────┐      │  │
                        │   │RabbitMQ  │  │Elastic-  │      │  │
                        │   │CloudAMQP │  │search    │      │  │
                        │   │(external)│  │(Elastic  │      │  │
                        │   └──────────┘  │Cloud)    │      │  │
                        │                 └──────────┘      │  │
                        │                                    │  │
                        │   ┌─────────────────────────────┐ │  │
                        │   │  Log Analytics Workspace     │ │  │
                        │   └─────────────────────────────┘ │  │
                        └────────────────────────────────────┘  │
                                                                  │
  GitHub ──► Actions ──► GHCR ──────────────────────────────────┘
             CI/CD        Images
```

### Deploy

```bash
# 1. Login
az login

# 2. Create resource group
az group create --name comments-rg --location westeurope

# 3. Deploy all infrastructure (Bicep IaC)
az deployment group create \
  --resource-group comments-rg \
  --template-file azure/main.bicep \
  --parameters postgresPassword='<STRONG_PASSWORD>'

# 4. Get live URLs
az deployment group show \
  --resource-group comments-rg \
  --name main \
  --query properties.outputs
```

### GitHub Secrets required for CI/CD

| Secret | Description |
|--------|-------------|
| `AZURE_CREDENTIALS` | `az ad sp create-for-rbac --sdk-auth` output |
| `AZURE_RESOURCE_GROUP` | `comments-rg` |

### Resources provisioned by Bicep

| Component | Azure service |
|-----------|--------------|
| API | Azure Container Apps |
| Frontend | Azure Container Apps |
| PostgreSQL | Azure Database for PostgreSQL – Flexible Server v17 |
| Redis | Azure Cache for Redis (Basic C0) |
| RabbitMQ | CloudAMQP (external) |
| Elasticsearch | Elastic Cloud on Azure |
| Logs | Azure Log Analytics Workspace |

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
