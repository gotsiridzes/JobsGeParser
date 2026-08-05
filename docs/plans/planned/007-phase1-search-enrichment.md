# 007 - Phase 1: Search & Enrichment (backend)

**Status:** Planned  
**Roadmap:** [Phase 1 - Search what's in the job](../../roadmap.md)

## Goals

- PostgreSQL full-text search over job title, company, and description (`simple` config + weighted `tsvector` + GIN)
- `pg_trgm` fuzzy fallback when FTS returns no rows
- Deterministic enrichment of six structured fields (salary, city, work mode, employment type, seniority, language)
- `GET /api/search` ranked seeker API; ops `POST .../enrichment/backfill`
- Store raw detail HTML alongside plain-text description for re-parse

## Decisions

- Public detail API keeps **full** `Description` text
- Enrichment backfill is **ops HTTP only** (no background worker)
- Frontend public UI (roadmap tickets 6–8) is out of scope

## Search approach

- Generated column `search_vector` with weights: Name `A`, Company `B`, Description `C` via `HasComputedColumnSql` (weighted; not plain `HasGeneratedTsVectorColumn`)
- Query: `websearch_to_tsquery('simple', q)` + `@@` + `ts_rank` ordering (Npgsql LINQ)
- Fallback: `similarity(Name|Company, q) >= 0.3` ordered by greatest similarity
- Existing `GET /api/jobs?q=` ILIKE path unchanged for ops console

## Key files

| Area | Path |
|------|------|
| Entity / DbContext | `Data/JobEntity.cs`, `Data/JobsDbContext.cs` |
| Migration | `Data/Migrations/*_AddSearchAndEnrichment.cs` |
| Enrichment | `Enrichment/EnrichmentService.cs` |
| Repo search / backfill | `Data/Repo.cs` |
| Scrape HTML | `Scraping/HtmlProcessor.cs`, `Scraping/JobsGeClient.cs` |
| Endpoints | `Endpoints/SearchEndpoints.cs`, `Endpoints/ScrapeEndpoints.cs` |
| DTOs | `Endpoints/Dtos/JobsApiDtos.cs` |
| Tests | `JobsGeParser.Tests/` |

## Verification

```bash
dotnet build backend/JobsGeParser/JobsGeParser.csproj
dotnet test backend/JobsGeParser.Tests/JobsGeParser.Tests.csproj
```

Manual: migrate DB, `GET /api/search?q=...`, `POST /api/jobs/scrape/enrichment/backfill` until `remaining` is 0.
