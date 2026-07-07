# 003 — Parallel scrape performance

**Status:** Implemented (2026-07-07)

## What shipped

- `DetailFetchConcurrency` — bounded parallel detail fetches via `Channel` + N consumers
- `ScrapeRequestThrottle` — global concurrency cap + `DetailPageDelayMs` between request starts
- `Repo.UpsertAndLinkCategoryAsync` — single `SaveChanges` for job upsert + category link
- `ScrapeProgressReporter` — progress DB writes every `ProgressUpdateInterval` jobs
- Per-consumer `IServiceScopeFactory` scope for thread-safe `DbContext`

## Config keys

| Key | Default |
|-----|---------|
| `DetailFetchConcurrency` | 3 |
| `DetailPageDelayMs` | 500 |
| `ProgressUpdateInterval` | 5 |

## Key files

- `JobsGeClient.cs`, `ScrapeRequestThrottle.cs`, `ScrapeProgressReporter.cs`
- `Repo.cs` (`UpsertAndLinkCategoryAsync`)
