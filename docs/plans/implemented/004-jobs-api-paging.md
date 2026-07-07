# 004 — Jobs API paging

**Status:** Implemented (2026-07-08)

## What shipped

- Paginated `GET /api/jobs` with `page`, `pageSize`, `category`, `q` filters
- `JobListItemDto` — list responses exclude descriptions (bounded payload size)
- `GET /api/jobs/{id}` — full job detail with description and category slugs
- `GET /api/jobs/dotnet` — same paging/filter params with `.net` title preset
- `DefaultJobsPageSize` / `MaxJobsPageSize` config with validation

## Breaking change

List endpoints return `JobsPageDto` (`items`, `totalCount`, `page`, `pageSize`, `totalPages`) instead of a bare JSON array.

## Config keys

| Key | Default |
|-----|---------|
| `DefaultJobsPageSize` | 20 |
| `MaxJobsPageSize` | 100 |

## Key files

- `JobsApiDtos.cs`, `Repo.cs` (`GetJobsPageAsync`, `GetJobByIdAsync`)
- `Endpoints/Jobs.cs`
