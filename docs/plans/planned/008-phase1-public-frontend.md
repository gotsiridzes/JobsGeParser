# 008 - Phase 1: Public frontend + ops split

**Status:** Planned  
**Roadmap:** [Phase 1 - Search what's in the job](../../roadmap.md)  
**Depends on:** [007 - Search & Enrichment backend](007-phase1-search-enrichment.md)

## Goals

- Split the SPA into **PublicLayout** (seeker) and **OpsLayout** (console under `/ops/*`)
- Ship search home, ranked results via `GET /api/search`, and attributed public job detail
- Keep existing scrape dashboard behavior under `/ops`

## Routes

| Route | Layout | Purpose |
|-------|--------|---------|
| `/` | Public | Search-first home |
| `/search` | Public | Ranked / browse results |
| `/jobs/:id` | Public | Seeker job detail |
| `/ops` … `/ops/jobs/:id` | Ops | Existing console |
| Legacy ops paths | | Redirect to `/ops/...` |

## Key files

| Area | Path |
|------|------|
| Routing | `frontend/src/App.tsx` |
| Layouts | `PublicLayout.tsx`, `OpsLayout.tsx` |
| Public pages | `frontend/src/pages/public/` |
| Ops pages | `frontend/src/pages/ops/` |
| Search API | `frontend/src/api/{types,client,hooks}.ts` |

## Verification

```bash
cd frontend && npm run build
```
