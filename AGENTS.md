# Agent guide

Monorepo with a .NET backend and a React frontend. Use the right README and rules for each project.

## Backend (API & scraper)

1. Read [backend/README.md](backend/README.md) for architecture and API
2. Follow [.cursor/rules/](.cursor/rules/) — `project-core`, `csharp-conventions`, `html-parsing`
3. Use [.cursor/skills/](.cursor/skills/) for endpoint and scraping workflows
4. Configure PostgreSQL in `backend/JobsGeParser/appsettings.Development.json`
5. Run: `dotnet run --project backend/JobsGeParser/JobsGeParser.csproj`
6. Test: `backend/JobsGeParser/JobsGeParser.http`
7. Plans archive: [docs/plans/](docs/plans/) (backend-focused)

## Frontend (dashboard UI)

1. Read [frontend/README.md](frontend/README.md) for routes and dev setup
2. Run API first (see above), then: `cd frontend && npm run dev`
3. UI proxies `/api` to `http://localhost:50423` via Vite dev server
4. Source: `frontend/src/` — pages, API client, components

## Both

- Repo overview: [README.md](README.md)
- Do not add backup `.csproj` files or unrelated CI workflows
