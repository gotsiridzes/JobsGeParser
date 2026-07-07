---
name: add-jobs-endpoint
description: Adds or modifies JobsGeParser minimal API endpoints under api/jobs/. Use when adding routes, filters, or HTTP handlers for job listings.
---

# Add JobsGeParser endpoint

## Steps

1. Add query/filter method on Repo if needed (mirror `ListDotnetApplications`)
2. Register route in `Endpoints/Jobs.cs` inside `RegisterJobsEndpoints`
3. Use `MapGroup` prefix `api/jobs/` — full path is group + route segment
4. Inject `Repo` or `JobsGeClient` via minimal API parameters
5. Return `Results.Ok(...)` for JSON responses
6. Add entry to `JobsGeParser.http` for manual test
7. Update `Readme.MD` API table if behavior is user-facing

## Template

```csharp
jobs.MapGet("segment", (Repo repo) => Results.Ok(repo.SomeMethod()));
```

## Example (existing pattern)

```csharp
// Repo.cs
public IEnumerable<JobApplication> ListDotnetApplications() =>
    _applications.Where(a => a.Name.ToLower().Contains(".net"));

// Endpoints/Jobs.cs
jobs.MapGet("dotnet", async (Repo repo) => Results.Ok(repo.ListDotnetApplications()));
```

## Files to touch

| Change | File |
|--------|------|
| New filter/query | `Repo.cs` |
| New route | `Endpoints/Jobs.cs` |
| Manual test | `JobsGeParser.http` |
| User-facing docs | `Readme.MD` |
