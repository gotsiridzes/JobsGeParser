---
name: add-jobs-endpoint
description: Adds or modifies JobsGeParser minimal API endpoints under api/jobs/. Use when adding routes, filters, or HTTP handlers for job listings.
---

# Add JobsGeParser endpoint

## Steps

1. Add async query method on `Repo` if needed (mirror `ListDotnetApplicationsAsync`)
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

## Example (existing pattern)

```csharp
// Repo.cs
public async Task<IReadOnlyList<JobApplication>> ListDotnetApplicationsAsync(CancellationToken ct = default) =>
    (await _db.Jobs.Where(j => EF.Functions.ILike(j.Name, "%.net%")).ToListAsync(ct))
        .Select(MapToDomain).ToList();

// Endpoints/Jobs.cs
jobs.MapGet("dotnet", async (Repo repo, CancellationToken ct) =>
    Results.Ok(await repo.ListDotnetApplicationsAsync(ct)));
```

## Files to touch

| Change | File |
|--------|------|
| New filter/query | `Repo.cs` |
| New route | `Endpoints/Jobs.cs` |
| Manual test | `JobsGeParser.http` |
| User-facing docs | `Readme.MD` |
