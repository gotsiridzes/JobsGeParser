# JobsGeParser

Monorepo for scraping [jobs.ge](https://jobs.ge/) listings and monitoring scrape operations.

| Project | Folder | Stack |
|---------|--------|-------|
| **API & scraper** | [`backend/`](backend/) | .NET 10, EF Core, PostgreSQL, background worker |
| **Dashboard UI** | [`frontend/`](frontend/) | Vite, React, TypeScript, TanStack Query |

Shared at repo root: [`docs/`](docs/) (backend implementation plans), [`.cursor/`](.cursor/) (AI rules & skills), [`.github/`](.github/) (CI).

## Quick start

**Terminal 1 — API**

```bash
dotnet run --project backend/JobsGeParser/JobsGeParser.csproj
```

**Terminal 2 — dashboard**

```bash
cd frontend
npm install
npm run dev
```

- API: `http://localhost:50423`
- UI: `http://localhost:5173` (proxies `/api` to the backend)

## Documentation

| Doc | Scope |
|-----|-------|
| [backend/README.md](backend/README.md) | Architecture, API endpoints, configuration, migrations |
| [frontend/README.md](frontend/README.md) | UI routes, build, dev proxy |
| [docs/roadmap.md](docs/roadmap.md) | Product roadmap — search, facets, alerts (next phase) |
| [docs/plans/](docs/plans/) | Backend implementation plan archive |
| [AGENTS.md](AGENTS.md) | Cursor / AI assistant entry point |

## Repository layout

```
JobsGeParser/                 # repository root
├── README.md                 # this file — monorepo overview
├── AGENTS.md                 # AI guide (both projects)
├── docs/                     # backend-focused plans
├── .cursor/                  # shared Cursor rules & skills
├── .github/                  # CI workflows
├── backend/
│   ├── README.md             # backend architecture & API
│   ├── JobsGeParser.sln
│   └── JobsGeParser/         # .NET project
└── frontend/
    ├── README.md             # dashboard UI
    ├── package.json
    └── src/
```

## Open in Visual Studio

Open [`backend/JobsGeParser.sln`](backend/JobsGeParser.sln). The frontend is a standalone npm project (not in the solution).

## Production (optional)

- **Single host:** `cd frontend && npm run build`, copy `frontend/dist` to `backend/JobsGeParser/wwwroot`, serve static files from ASP.NET
- **Separate hosts:** deploy API and UI independently; add CORS on the backend for the UI origin
