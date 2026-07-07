---
name: scrape-jobs-ge
description: Modifies jobs.ge scraping, HtmlAgilityPack selectors, or JobsGeClient fetch flow. Use when jobs.ge HTML changes, new fields are extracted, category scraping, parallel scrape, or background scrape behavior is updated.
---

# Scrape jobs.ge

## Files

| File | Responsibility |
|------|----------------|
| `HtmlProcessor.cs` | List + description parsing |
| `JobsGeClient.cs` | Job Channel + parallel consumers, per-job scoped Repo |
| `ScrapeRequestThrottle.cs` | Global concurrency cap + min delay between HTTP requests |
| `ScrapeProgressReporter.cs` | Throttled `scrape_runs` progress updates |
| `Workers/JobScrapeWorker.cs` | Category Channel + parallel category consumers per tick |
| `Workers/ScrapeWorkerState.cs` | Live tick state, `activeCategoryRuns` |
| `CategorySync.cs` | Sync categories from appsettings to DB on startup |
| `JobsGeParserOptions` | Categories[], category/job concurrency, delays |
| `Repo.cs` | `UpsertAndLinkCategoryAsync` (single SaveChanges) |

## Workflow

1. Confirm live HTML structure on jobs.ge (listing + detail page)
2. Update selectors in `HtmlProcessor` only
3. Add new categories in appsettings `Categories` array with slug, name, listUrl
4. Tune `CategoryScrapeConcurrency`, `DetailFetchConcurrency`, and `DetailPageDelayMs` for jobs.ge rate limits
5. Verify via `GET /api/jobs/scrape/overview` and `GET /api/jobs?category={slug}`

## Scrape flow (two-level parallelism)

```
JobScrapeWorker (PeriodicTimer)
  → Channel of enabled categories (M parallel consumers)
      per category (own DI scope):
        StartScrapeRun(categorySlug, batchId)
        → JobsGeClient.ScrapeCategoryAsync
            → GET listing (throttled) → parse → job Channel
            → N job consumers in parallel:
                GET detail (throttled) → UpsertAndLinkCategoryAsync (scoped Repo)
                → throttled progress update
        → CompleteScrapeRun
```

## Parallelism rules

- **Category level:** each category consumer creates its own `IServiceScope` — never share `Repo`/`JobsGeClient` across category tasks
- **Job level:** each job consumer inside `JobsGeClient` creates its own scope (same rule)
- `ScrapeRequestThrottle` is a **singleton** — all categories and detail workers share one HTTP budget
- Do not remove throttle when increasing concurrency
- Only one app instance should scrape (`ScrapeEnabled: true`) against a given database

## Reference

See `Readme.MD` sections **Scrape flow** and **Configuration**.
