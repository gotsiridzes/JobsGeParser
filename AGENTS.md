# Agent guide

Monorepo with a .NET backend and a React frontend. Use the right README and rules for each project.

## Backend (API & scraper)

1. Read [backend/README.md](backend/README.md) for architecture and API
2. Follow [.cursor/rules/](.cursor/rules/) — `project-core`, `csharp-conventions`, `html-parsing`
3. Use [.cursor/skills/](.cursor/skills/) for endpoint and scraping workflows
4. Configure PostgreSQL in `backend/JobsGeParser/appsettings.Development.json` (requires [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0))
5. Product roadmap: [docs/roadmap.md](docs/roadmap.md); implementation plans: [docs/plans/](docs/plans/)

### Local dev (user runs manually)

- API: `dotnet run --project backend/JobsGeParser/JobsGeParser.csproj`
- HTTP file: `backend/JobsGeParser/JobsGeParser.http`

### After agent code changes

- Verify with **build only** (see `project-core` rule). Do not start `dotnet run` or background scrape workers unless the user asks.

## Frontend (dashboard UI)

1. Read [frontend/README.md](frontend/README.md) for routes and dev setup
2. Source: `frontend/src/` — pages, API client, components
3. UI proxies `/api` to `http://localhost:50423` via Vite dev server

### Local dev (user runs manually)

- Run API first (see backend section), then: `cd frontend && npm run dev`

### After agent code changes

- When frontend files change: `cd frontend && npm run build`. Do not start `npm run dev` unless the user asks.

## Both

- Repo overview: [README.md](README.md)
- Writing style: no em dashes (`—`) in code, docs, UI copy, or comments; see `.cursor/rules/writing-style.mdc`
- Do not add backup `.csproj` files or unrelated CI workflows
