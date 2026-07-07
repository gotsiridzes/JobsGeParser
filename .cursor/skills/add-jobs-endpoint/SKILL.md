---
name: add-jobs-endpoint
description: Adds or modifies JobsGeParser minimal API endpoints under api/jobs/. Use when adding routes, filters, or HTTP handlers for job listings.
---

# Add JobsGeParser endpoint

## Steps

1. Add async query method on `Repo` if needed (mirror `GetJobsPageAsync`)
2. Register route in `Endpoints/Jobs.cs` inside `RegisterJobsEndpoints`
3. Use `MapGroup` prefix `api/jobs/` — full path is group + route segment
4. Inject `Repo` via minimal API parameters (read-only endpoints only)
5. Return `Results.Ok(...)` for JSON responses
6. Add entry to `JobsGeParser.http` for manual test
7. Update `Readme.MD` API table if behavior is user-facing

## Template

```csharp
jobs.MapGet("segment", async (Repo repo, CancellationToken ct) =>
    Results.Ok(await repo.SomeMethodAsync(ct)));
```

## Paging pattern (list endpoints)

List endpoints return `JobsPageDto` — never load all jobs or descriptions in one response.

```csharp
// Repo.cs
public async Task<JobsPageDto> GetJobsPageAsync(
    JobQuery query, int page, int pageSize, CancellationToken ct = default)

// Endpoints/Jobs.cs
jobs.MapGet("", async (
    Repo repo,
    JobsGeParserOptions options,
    string? category,
    string? q,
    int? page,
    int? pageSize,
    CancellationToken ct) =>
    Results.Ok(await repo.GetJobsPageAsync(
        new JobQuery(category, q, DotNetOnly: false),
        page ?? 1,
        pageSize ?? options.DefaultJobsPageSize,
        ct)));
```

- `page` — 1-based, default 1
- `pageSize` — default `DefaultJobsPageSize`, clamped to `MaxJobsPageSize`
- List items use `JobListItemDto` (no description)
- Full job: `GET /api/jobs/{id}` → `JobDetailDto`

## Files to touch

| Change | File |
|--------|------|
| New filter/query | `Repo.cs` |
| New route | `Endpoints/Jobs.cs` |
| DTOs | `JobsApiDtos.cs` |
| Manual test | `JobsGeParser.http` |
| User-facing docs | `Readme.MD` |
