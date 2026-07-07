# 005 — Parallel category scraping

**Status:** Implemented (2026-07-08)

## What shipped

- `CategoryScrapeConcurrency` — bounded parallel category scrapes per tick via `Channel` + N consumers in `JobScrapeWorker`
- Per-category DI scope (`Repo`, `JobsGeClient`) for thread-safe `DbContext`
- `ScrapeWorkerState.ActiveCategoryRuns` — live snapshot of multiple in-flight category runs
- `CurrentCategorySlug` / `CurrentRunId` populated only when exactly one category is active (backward compatible)

## Why this was deferred in plan 003

Plan 003 intentionally scoped parallel work to **jobs within a category** only. Parallel categories were discussed but marked out of scope for a minimal first performance pass.

## Config keys

| Key | Default |
|-----|---------|
| `CategoryScrapeConcurrency` | 5 |

HTTP remains globally throttled by `ScrapeRequestThrottle` (`DetailFetchConcurrency` + `DetailPageDelayMs`).

## Key files

- `Workers/JobScrapeWorker.cs`, `Workers/ScrapeWorkerState.cs`
- `JobsGeParserOptions.cs`
