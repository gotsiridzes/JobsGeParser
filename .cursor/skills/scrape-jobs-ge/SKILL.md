---
name: scrape-jobs-ge
description: Modifies jobs.ge scraping, HtmlAgilityPack selectors, or JobsGeClient fetch flow. Use when jobs.ge HTML changes, new fields are extracted, or retrieve behavior is updated.
---

# Scrape jobs.ge

## Files

| File | Responsibility |
|------|----------------|
| `HtmlProcessor.cs` | List + description parsing |
| `JobsGeClient.cs` | HTTP + channel batching + `Repo.Save` |
| `Ext.cs` | Georgian date parsing |
| `JobsGeParserOptions` | `BaseUrl`, `JobsListUrl` |
| `JobApplication.cs` | Domain model for extracted fields |

## Workflow

1. Confirm live HTML structure on jobs.ge (listing + detail page)
2. Update selectors in `HtmlProcessor` only
3. Map new fields on `JobApplication` with private setters + setter methods
4. Keep 500ms `Task.Delay` between detail requests unless config is added to `JobsGeParserOptions`
5. Test via `POST /api/jobs/retrieve` then `GET /api/jobs/`

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

## Retrieve flow

```
GET JobsListUrl → parse listing → Channel<JobApplication>
  → foreach job: GET detail → ParseDescription → Repo.Save → delay 500ms
```

## Reference

See `Readme.MD` sections **jobs.ge HTML selectors** and **Data flow: POST api/jobs/retrieve**.
