---
name: scrape-jobs-ge
description: Modifies jobs.ge scraping, HtmlAgilityPack selectors, or JobsGeClient fetch flow. Use when jobs.ge HTML changes, new fields are extracted, or background scrape behavior is updated.
---

# Scrape jobs.ge

## Files

| File | Responsibility |
|------|----------------|
| `HtmlProcessor.cs` | List + description parsing |
| `JobsGeClient.cs` | HTTP scrape loop + `Repo.UpsertAsync` |
| `Workers/JobScrapeWorker.cs` | Periodic background scrape scheduling |
| `Ext.cs` | Georgian date parsing + DI |
| `JobsGeParserOptions` | URLs, scrape interval, delays |
| `JobApplication.cs` | Domain model for extracted fields |
| `Repo.cs` | Idempotent upsert to PostgreSQL |

## Workflow

1. Confirm live HTML structure on jobs.ge (listing + detail page)
2. Update selectors in `HtmlProcessor` only
3. Map new fields on `JobApplication` + `JobEntity` with migration if persisted
4. Use `DetailPageDelayMs` from options for rate limiting
5. Verify via `GET /api/jobs/scrape/status` and `GET /api/jobs/`

## Scrape flow

```
JobScrapeWorker (PeriodicTimer)
  → scope → JobsGeClient.ScrapeAsync
      → GET listing → parse → foreach job:
          GET detail → ParseDescription → Repo.UpsertAsync → delay
  → record scrape_runs row
```

## Selectors (current)

**Listing** — `ParseHtmlAndGetJobApplicationsList`:

- `//html//body//div[@class='regularEntries']//table`
- Rows: skip header, require >1 `<td>`
- Cell value: `innerText|first anchor href`

**Detail** — `ParseDescription`:

- `#job` → `//table[@class='dtable']` → 4th `<tr>` inner text

**Dates** — `Ext.GetDate`:

- Format: `"<day> <georgian_month>"` with current year
- Months: `იანვარი` … `დეკემბერი`

## Reference

See `Readme.MD` sections **Scrape flow**, **Idempotent upsert**, and **jobs.ge HTML selectors**.
