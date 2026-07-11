---
name: scrape-jobs-ge
description: Modifies jobs.ge scraping, HtmlAgilityPack selectors, or JobsGeClient fetch flow. Use when jobs.ge HTML changes, new fields are extracted, category scraping, parallel scrape, or background scrape behavior is updated.
---

# Scrape jobs.ge

## Files

| File | Responsibility |
|------|----------------|
| `Scraping/HtmlProcessor.cs` | List + description parsing |
| `Scraping/JobsGeClient.cs` | Listing pagination (`for_scroll`), job Channel + parallel consumers, per-job scoped Repo |
| `Scraping/ListingUrlBuilder.cs` | Rewrites category ListUrl `page` / `for_scroll` query params |
| `Scraping/ScrapeRequestThrottle.cs` | Global concurrency cap + min delay between HTTP requests |
| `Scraping/ScrapeProgressReporter.cs` | Throttled `scrape_runs` progress updates |
| `Scraping/GeorgianDateExtensions.cs` | Georgian month name → DateOnly parsing |
| `Scraping/CategorySync.cs` | Sync categories from appsettings to DB on startup |
| `Workers/JobScrapeWorker.cs` | Category Channel + parallel category consumers per tick; abandons orphaned Running runs on start/stop |
| `Workers/ScrapeWorkerState.cs` | Live tick state, `activeCategoryRuns` |
| `Configuration/JobsGeParserOptions.cs` | Categories[], category/job concurrency, delays |
| `Data/Repo.cs` | `UpsertAndLinkCategoryAsync` (single SaveChanges) |

## Workflow

1. Confirm live HTML structure on jobs.ge (listing + detail page)
2. Update selectors in `Scraping/HtmlProcessor` only
3. Add new categories in appsettings `Categories` array with slug, name, listUrl
4. Tune `CategoryScrapeConcurrency`, `DetailFetchConcurrency`, and `DetailPageDelayMs` for jobs.ge rate limits
5. Verify with `dotnet build backend/JobsGeParser/JobsGeParser.csproj`. Live scrape checks (`GET /api/jobs/scrape/overview`, etc.) are manual only unless the user asks to run the app.

## Scrape flow (two-level parallelism)

```
JobScrapeWorker (PeriodicTimer)
  → Channel of enabled categories (M parallel consumers)
      per category (own DI scope):
        StartScrapeRun(categorySlug, batchId)
        → JobsGeClient.ScrapeCategoryAsync
            → GET listing pages until empty (page 1 + for_scroll; throttled) → parse → job Channel
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

See `backend/README.md` sections **Scrape flow** and **Configuration**.
