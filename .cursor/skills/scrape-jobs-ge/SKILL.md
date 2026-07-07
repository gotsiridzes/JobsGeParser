---
name: scrape-jobs-ge
description: Modifies jobs.ge scraping, HtmlAgilityPack selectors, or JobsGeClient fetch flow. Use when jobs.ge HTML changes, new fields are extracted, category scraping, parallel scrape, or background scrape behavior is updated.
---

# Scrape jobs.ge

## Files

| File | Responsibility |
|------|----------------|
| `HtmlProcessor.cs` | List + description parsing |
| `JobsGeClient.cs` | Channel + parallel consumers, per-job scoped Repo |
| `ScrapeRequestThrottle.cs` | Global concurrency cap + min delay between HTTP requests |
| `ScrapeProgressReporter.cs` | Throttled `scrape_runs` progress updates |
| `Workers/JobScrapeWorker.cs` | Loop enabled categories per tick |
| `CategorySync.cs` | Sync categories from appsettings to DB on startup |
| `JobsGeParserOptions` | Categories[], concurrency, delays, progress interval |
| `Repo.cs` | `UpsertAndLinkCategoryAsync` (single SaveChanges) |

## Workflow

1. Confirm live HTML structure on jobs.ge (listing + detail page)
2. Update selectors in `HtmlProcessor` only
3. Add new categories in appsettings `Categories` array with slug, name, listUrl
4. Tune `DetailFetchConcurrency` and `DetailPageDelayMs` for jobs.ge rate limits
5. Verify via `GET /api/jobs/scrape/overview` and `GET /api/jobs?category={slug}`

## Scrape flow

```
JobScrapeWorker (PeriodicTimer)
  → foreach enabled category:
      StartScrapeRun(categorySlug, batchId)
      → JobsGeClient.ScrapeCategoryAsync
          → GET listing (throttled) → parse → Channel
          → N consumers in parallel:
              GET detail (throttled) → UpsertAndLinkCategoryAsync (scoped Repo)
              → throttled progress update
      → CompleteScrapeRun
```

## Parallelism rules

- Each consumer creates its own `IServiceScope` — never share `Repo`/`DbContext` across tasks
- `ScrapeRequestThrottle` wraps all listing and detail HTTP calls
- Do not remove throttle when increasing concurrency

## Reference

See `Readme.MD` sections **Scrape flow** and **Configuration**.
