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
| `/alerts/new` | Job alert builder with live match preview (`?q`, `?category` prefill it) |
| `/a/c/:token` | Confirmation link target from the confirmation email |
| `/a/:token` | Manage alerts: pause, resume, edit, delete, unsubscribe all |
| `/a/u/:token` | Unsubscribe page (asks before acting) |

### Alerts

Users save a filter against an email address and get notified about new matching jobs. There are no accounts: the links in each email carry a capability token, which the app sends in an `X-Alert-Token` header and never stores. The whole feature is hidden when the backend runs with `Alerts:Enabled=false`.

With the backend on its default console channel, the confirmation link is printed to the API log instead of being emailed. Open it on `http://localhost:5173/a/c/<token>`.

Entry points are the "Alert me about new jobs like these" button on `/search` and `/alerts/new` directly.

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
