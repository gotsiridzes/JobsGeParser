# 002 — Multi-category job scraping

**Status:** Implemented (2026-07-07)

## What shipped

- `Categories[]` in appsettings replaces single `JobsListUrl`
- Tables: `categories`, `job_categories`; `CategorySlug` on `scrape_runs`
- `CategorySync` on startup — syncs config to DB, backfills existing jobs to `it`
- One scrape run per enabled category per worker tick
- `JobsGeClient.ScrapeCategoryAsync` with per-category listing URL
- APIs: `GET /api/jobs/categories`, `?category=slug`, per-category scrape status

## Design decisions

| Decision | Choice |
|----------|--------|
| Category source | appsettings |
| Job–category relation | Many-to-many |
| Scrape scope | One scrape run per category |

## Key files

- `JobCategoryOptions.cs`, `CategorySync.cs`
- `Data/CategoryEntity.cs`, `Data/JobCategoryEntity.cs`
- `Repo.cs`, `JobsGeClient.cs`, `Workers/JobScrapeWorker.cs`, `Endpoints/Jobs.cs`
- Migration: `AddJobCategories`
