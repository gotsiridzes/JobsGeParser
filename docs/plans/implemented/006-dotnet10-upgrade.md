# 006 — .NET 10 upgrade

**Status:** Implemented (2026-07-08)

## What shipped

- Target framework `net10.0` with C# 14 (`LangVersion` 14)
- EF Core / Npgsql packages upgraded to 10.0.x (EF 10.0.9, Npgsql 10.0.2)
- Repo root `global.json` pins SDK 10.0.301
- Options pattern: `BindConfiguration`, `ValidateOnStart`, `IValidateOptions<T>` validators
- All options consumers migrated from raw singleton to `IOptions<T>`
- Config-driven CORS policy `"Frontend"` via `Cors:AllowedOrigins`
- OpenAPI 3.1 in Development (`AddOpenApi`, `MapOpenApi`, endpoint tags/names/summaries)
- C# 14 `field`-backed property on `JobsGeParserOptions.MaxJobsPageSize`

## Config keys

| Key | Default |
|-----|---------|
| `Cors.AllowedOrigins` | `[ "http://localhost:5173" ]` |

## Key files

- `JobsGeParser.csproj`, `global.json`, `Program.cs`
- `Configuration/DependencyInjection.cs`, `JobsGeParserOptionsValidator.cs`, `CorsOptions.cs`
- `Endpoints/JobsEndpoints.cs`, `Endpoints/ScrapeEndpoints.cs`
- `.github/workflows/dotnet.yml`

## Breaking change

Requires .NET 10 SDK and runtime. No API contract changes.
