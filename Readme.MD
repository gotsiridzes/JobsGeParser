# JobsGeParser

A minimal ASP.NET Core API that scrapes [jobs.ge](https://jobs.ge/) IT job listings, fetches each job's description, and stores results in an in-memory repository.

## Tech stack

| Layer | Choice |
|-------|--------|
| Runtime | .NET 8 (`net8.0`) |
| API style | Minimal hosting (`WebApplication`) + extension methods |
| HTML parsing | [HtmlAgilityPack](https://html-agility-pack.net/) 1.11.x |
| Storage | In-memory `List<JobApplication>` (no persistence) |
| CI | GitHub Actions — `dotnet publish` on `master` |

## Solution layout

```
JobsGeParser/
├── JobsGeParser.sln          # Single-project solution; Readme.MD linked as solution item
├── Readme.MD
├── .github/workflows/
│   ├── dotnet.yml            # Primary CI — build & publish win-x64 artifact
│   └── dotnet-desktop.yml    # Legacy template (not wired to this project)
└── JobsGeParser/
    ├── Program.cs            # Host bootstrap
    ├── Ext.cs                # DI registration + Georgian date parsing
    ├── JobsGeParserOptions.cs
    ├── JobsGeClient.cs       # HTTP fetch + batch description loading
    ├── HtmlProcessor.cs      # jobs.ge HTML → JobApplication
    ├── Repo.cs               # In-memory store
    ├── JobApplication.cs     # Domain model
    ├── Endpoints/Jobs.cs     # API route registration (class name: Subscriptions)
    ├── appsettings.json
    ├── JobsGeParser.http     # REST Client smoke tests
    └── Properties/launchSettings.json
```

## Architecture

```
HTTP request
    → Endpoints/Jobs.cs (RegisterJobsEndpoints)
        → Repo (read)  OR  JobsGeClient (retrieve)
            → HtmlProcessor (parse list + description pages)
            → Repo.Save (write)
```

### Dependency injection (`Ext.AddJobsGeService`)

- `JobsGeParserOptions` — singleton, bound from `JobsGeParserOptions` config section
- Named `HttpClient` `"JobsGeClient"` with `BaseAddress` = `BaseUrl`
- Singletons: `JobsGeClient`, `HtmlProcessor`, `Repo`

### Data flow: `POST api/jobs/retrieve`

1. `JobsGeClient` GETs `JobsListUrl` (IT category listing).
2. `HtmlProcessor.ParseHtmlAndGetJobApplicationsList` extracts rows from the listing table.
3. Each `JobApplication` is written to a bounded `Channel<JobApplication>` (capacity 20).
4. A background loop reads from the channel, GETs each job detail page, parses description, saves to `Repo`, then delays 500 ms between requests.

### Domain model: `JobApplication`

| Property | Source |
|----------|--------|
| `Id` | Parsed from job link query `id=` |
| `Name`, `Link` | First table column (text + anchor href) |
| `Company`, `CompanyLink` | Second column |
| `Published`, `EndDate` | Third and fourth columns (Georgian month names → `DateOnly`) |
| `Description` | Detail page: `#job` → `table.dtable` → 4th `<tr>` inner text |

## Configuration

`appsettings.json`:

```json
{
  "JobsGeParserOptions": {
    "BaseUrl": "https://jobs.ge/",
    "JobsListUrl": "?page=1&q=&cid=6&lid=1&jid=1"
  }
}
```

| Setting | Purpose |
|---------|---------|
| `BaseUrl` | HttpClient base address for relative links |
| `JobsListUrl` | Listing path/query — `cid=6` is the IT category |

Override via environment variables using the standard `JobsGeParserOptions__*` prefix.

## API endpoints

Base URL (development): `http://localhost:50423`

| Method | Route | Behavior |
|--------|-------|----------|
| `GET` | `/api/jobs/` | Returns all jobs currently in `Repo` |
| `POST` | `/api/jobs/retrieve` | Scrapes jobs.ge, fetches descriptions, populates `Repo`. Returns `200 OK` with empty body. |
| `GET` | `/api/jobs/dotnet` | Jobs whose `Name` contains `.net` (case-insensitive) |

Use `JobsGeParser/JobsGeParser.http` for manual testing.

**Typical workflow:** call `POST /api/jobs/retrieve` first (may take minutes due to per-job delay), then `GET /api/jobs/` or `GET /api/jobs/dotnet`.

## jobs.ge HTML selectors

These XPath/CSS assumptions are fragile — verify against live HTML when jobs.ge changes layout.

**Listing page** (`ParseHtmlAndGetJobApplicationsList`):

- Table: `//html//body//div[@class='regularEntries']//table`
- Rows: skip header, require >1 `<td>`
- Cell format: `innerText` plus first `<a>` href, joined as `text|href`

**Detail page** (`ParseDescription`):

- Container: `#job`
- Description row: `//table[@class='dtable']` → 4th `<tr>` (0-based index 3)

**Dates** (`Ext.GetDate`):

- Format: `"<day> <georgian_month>"` using current calendar year
- Months: Georgian names (`იანვარი` … `დეკემბერი`) mapped in `Ext.GetMonth`

## Development

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Run locally

```bash
cd JobsGeParser
dotnet run --project JobsGeParser/JobsGeParser.csproj
```

### Build

```bash
dotnet build JobsGeParser.sln
dotnet publish JobsGeParser/JobsGeParser.csproj -c Release
```

CI publishes a self-contained `win-x64` artifact via `.github/workflows/dotnet.yml`.

## Conventions for future changes

Follow existing patterns unless deliberately refactoring:

1. **Namespaces** — root `JobsGeParser`, endpoints under `JobsGeParser.Endpoints`.
2. **DI** — register new services in `Ext.AddJobsGeService`; keep `Program.cs` thin.
3. **Endpoints** — add routes in `Endpoints/Jobs.cs` via `RegisterJobsEndpoints` extension on `IEndpointRouteBuilder`; use `MapGroup("api/jobs/")`.
4. **Parsing** — keep HTML logic in `HtmlProcessor`; HTTP orchestration in `JobsGeClient`.
5. **Options** — new config keys go on `JobsGeParserOptions` + `appsettings.json` + validation in `Ext`.
6. **Global usings** — only `System` is global; add sparingly.
7. **Nullable** — enabled project-wide; respect nullability on new code.

### Adding a new filtered endpoint

Example pattern (see `ListDotnetApplications`):

```csharp
// Repo.cs
public IEnumerable<JobApplication> ListByKeyword(string keyword) =>
    _applications.Where(a => a.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase));

// Endpoints/Jobs.cs
jobs.MapGet("keyword/{term}", (Repo repo, string term) =>
    Results.Ok(repo.ListByKeyword(term)));
```

### Adding persistence

Replace or wrap `Repo` with a real store (SQLite, PostgreSQL, etc.). Keep the same public surface (`Save`, query methods) so `JobsGeClient` and endpoints change minimally.

### Rate limiting / scraping etiquette

`JobsGeClient` uses a fixed 500 ms delay between detail-page requests. Increase if jobs.ge throttles; consider configurable delay in `JobsGeParserOptions`.

## Known limitations and technical debt

| Item | Notes |
|------|-------|
| In-memory storage | Data lost on app restart; not shared across instances |
| No error handling on HTTP/HTML | Failed parses or 404s can throw; no retry |
| Static `HttpClient` field in `JobsGeClient` | Should use instance field from factory |
| `content.Replace(...)` not assigned | Line 54 in `JobsGeClient.cs` — replacement has no effect |
| `ValidateRapidApiSdkOptions` | Misnamed; validates `JobsGeParserOptions` only |
| `Endpoints/Jobs.cs` class name | Class is `Subscriptions`, file is `Jobs.cs` |
| Backup `.csproj` files | `JobsGeParser - Backup*.csproj` — safe to delete |
| `dotnet-desktop.yml` | Unused WPF/MSIX template; `Wap_Project_Directory` unset |

## AI assistant context

Project-specific Cursor rules and skills live under `.cursor/` (see repository instructions for setup). Agents should read this README and those files before implementing features involving jobs.ge scraping, HTML selectors, or API endpoints.
