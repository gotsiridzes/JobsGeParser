# JobsGeParser UI

Vite + React + TypeScript app with two experiences in one deployable:

- **Public seeker** (`/`, `/search`, `/jobs/:id`): description search and job detail
- **Ops console** (`/ops/*`): scrape dashboard and job browser

Part of the monorepo. Backend docs: [`../backend/README.md`](../backend/README.md), repo overview: [`../README.md`](../README.md).

Listings originate from [jobs.ge](https://jobs.ge); the UI always links back to the original posting.

## Prerequisites

- Node.js 18+ (20+ recommended)
- JobsGeParser API running at `http://localhost:50423`

## Development

Terminal 1 - start the API (from repo root):

```bash
dotnet run --project backend/JobsGeParser/JobsGeParser.csproj
```

Terminal 2 - start the UI:

```bash
cd frontend
npm install
npm run dev
```

Open [http://localhost:5173](http://localhost:5173). API requests to `/api/*` are proxied to the .NET backend.

## Pages

### Public

| Route | Purpose |
|-------|---------|
| `/` | Search-first home, category chips, latest jobs |
| `/search` | Ranked FTS results (`?q`) or category browse (`?category`) |
| `/jobs/:id` | Public job detail with structured chips and jobs.ge link |

### Ops

| Route | Purpose |
|-------|---------|
| `/ops` | Live scrape worker dashboard |
| `/ops/runs` | Paginated scrape run history |
| `/ops/batches/:batchId` | All category runs in one scrape tick |
| `/ops/categories` | Category health grid |
| `/ops/jobs` | Ops job browser (ILIKE title/company) |
| `/ops/jobs/:id` | Ops job detail |

Legacy paths (`/runs`, `/categories`, `/jobs`, `/batches/:id`) redirect under `/ops`.

## Build

```bash
npm run build
npm run preview
```

Production hosting options are documented in the root [README.md](../README.md).
