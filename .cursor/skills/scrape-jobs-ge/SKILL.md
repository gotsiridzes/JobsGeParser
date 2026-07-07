---
name: scrape-jobs-ge
description: Modifies jobs.ge scraping, HtmlAgilityPack selectors, or JobsGeClient fetch flow. Use when jobs.ge HTML changes, new fields are extracted, category scraping, or background scrape behavior is updated.
---

# Scrape jobs.ge

## Files

| File | Responsibility |
|------|----------------|
| `HtmlProcessor.cs` | List + description parsing |
| `JobsGeClient.cs` | Per-category HTTP scrape + upsert + category link |
| `Workers/JobScrapeWorker.cs` | Loop enabled categories per tick |
| `CategorySync.cs` | Sync categories from appsettings to DB on startup |
| `JobsGeParserOptions` | BaseUrl, Categories[], scrape interval, delays |
| `Repo.cs` | Upsert, LinkJobToCategoryAsync, category-filtered queries |

## Workflow

1. Confirm live HTML structure on jobs.ge (listing + detail page)
2. Update selectors in `HtmlProcessor` only
3. Add new categories in appsettings `Categories` array with slug, name, listUrl
4. Use `DetailPageDelayMs` from options for rate limiting
5. Verify via `GET /api/jobs/scrape/status/{slug}` and `GET /api/jobs?category={slug}`

## Scrape flow

```
JobScrapeWorker (PeriodicTimer)
  → foreach enabled category:
      StartScrapeRun(categorySlug)
      → JobsGeClient.ScrapeCategoryAsync
          → GET category.ListUrl → parse → foreach job:
              GET detail → UpsertAsync → LinkJobToCategoryAsync → delay
      → CompleteScrapeRun
```

## Reference

See `Readme.MD` sections **Scrape flow**, **Job–category model**, and **Configuration**.
