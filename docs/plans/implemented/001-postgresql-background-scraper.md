# 001 — PostgreSQL + background job scraper

**Status:** Implemented (2026-07-07)

## What shipped

- PostgreSQL persistence via EF Core 8 + Npgsql
- Idempotent job upserts keyed on jobs.ge `Id` (insert / skip / update)
- `JobScrapeWorker` background scraper with configurable interval
- Removed `POST /api/jobs/retrieve`
- `scrape_runs` table for scrape history
- Live scrape progress via `UpdateScrapeRunProgressAsync` (post-plan fix)

## Goals

1. **PostgreSQL** — persist jobs with idempotent upserts keyed on jobs.ge `Id`
2. **Background worker** — automatic periodic scraping

## Key files

- `Data/JobsDbContext.cs`, `Data/JobEntity.cs`, `Data/ScrapeRunEntity.cs`
- `Repo.cs`, `JobsGeClient.cs`, `Workers/JobScrapeWorker.cs`
- `Endpoints/Jobs.cs`, `Program.cs`, `Ext.cs`

## Verification

- Migrations: `InitialJobs`
- Endpoints: `GET /api/jobs/`, `GET /api/jobs/dotnet`, `GET /api/jobs/scrape/status`
- Data survives app restart
