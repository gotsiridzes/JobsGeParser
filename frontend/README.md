# JobsGeParser Dashboard UI

Internal operations console for the JobsGeParser API. Built with Vite, React, TypeScript, TanStack Query, and Tailwind CSS.

Part of the monorepo — backend docs: [`../backend/README.md`](../backend/README.md), repo overview: [`../README.md`](../README.md).

## Prerequisites

- Node.js 18+ (20+ recommended)
- JobsGeParser API running at `http://localhost:50423`

## Development

Terminal 1 — start the API (from repo root):

```bash
dotnet run --project backend/JobsGeParser/JobsGeParser.csproj
```

Terminal 2 — start the UI:

```bash
cd frontend
npm install
npm run dev
```

Open [http://localhost:5173](http://localhost:5173). API requests to `/api/*` are proxied to the .NET backend.

## Pages

| Route | Purpose |
|-------|---------|
| `/` | Live scrape worker dashboard (polls overview every 5–30s) |
| `/runs` | Paginated scrape run history with filters |
| `/batches/:batchId` | All category runs in one scrape tick |
| `/categories` | Category health grid with job counts |
| `/jobs` | Browse scraped jobs with search and category filter |
| `/jobs/:id` | Job detail with full description |

## Build

```bash
npm run build
npm run preview
```

Production hosting options are documented in the root [README.md](../README.md).
