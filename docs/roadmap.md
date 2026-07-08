# JobsGeParser — Next Phase Roadmap

**Status:** Approved direction (2026-07-08)  
**Scope:** Product evolution from ops console → job seeker tool (search, facets, alerts)  
**Implementation plans:** Phase tickets will be broken out under [`docs/plans/planned/`](plans/planned/) as work starts.

---

## TL;DR: Recommended next 90 days

- **Weeks 1–4 (Phase 1 — "Search what's *in* the job"):** Add PostgreSQL full-text search (`tsvector('simple')` + GIN) over **descriptions**, plus a `pg_trgm` fallback for typo/substring on Georgian+English. Extract 6 high-value structured fields via deterministic rules (salary, location, remote/onsite, employment type, seniority, language). Ship a **public search page** — the first non-ops UI.
- **Weeks 5–8 (Phase 2 — "Freshness & faceted filters"):** Faceted filtering on the new structured fields, "new since your last visit," and explicit **expiry/active** state. Introduce **saved searches** via anonymous shareable tokens (no full auth yet).
- **Weeks 9–12 (Phase 3 — "Alerts"):** A **Telegram bot** as the default alert channel (identity + delivery for free, huge in Georgia), email digest as secondary. Background matching + dedup engine on saved searches.
- **Cross-cutting:** Split the React app into a **public seeker experience** and the existing **ops console** using two route groups in one deployable app.
- **One rule for the whole quarter:** every ticket must move a user-facing metric *and* teach one named backend/DB topic. No plumbing for its own sake.

---

## 1. Product vision

**Primary user:** the active Georgian job seeker — disproportionately in tech, finance, and professional roles — who already uses jobs.ge but is frustrated that its search is essentially title-and-company matching inside a single category silo. Secondary user: the *passive* seeker who doesn't want to check daily and would rather be pinged when something matches.

**The pain we solve better than jobs.ge:** On the official site you search one category at a time, you can't search the text *inside* a posting, and you can't filter by the things that actually decide whether you apply — salary, remote/onsite, seniority, required language, employment type. There are no alerts, no "what's new since I last looked," and no cross-category aggregation. Our scraper already normalizes every listing into one store, so we can offer **description-level search across all 16 categories at once, with structured filters, freshness signals, and alerts** — none of which the native site does well.

**One-sentence pitch:** *Search every jobs.ge listing by what's actually in the job — salary, remote, seniority, skills — across all categories at once, and get pinged the moment a match appears.*

---

## 2. Gap analysis

| User need | Current capability | Gap | Proposed solution |
|---|---|---|---|
| Find jobs by *content*, not just title | `ILIKE` on `Name`, `Company` | Descriptions are unsearchable; no ranking | Postgres FTS (`tsvector` + GIN) over description; `ts_rank` ordering |
| Multi-word / fuzzy / typo queries | Single `%q%` substring | No AND/OR, no phrase, no typo tolerance | `websearch_to_tsquery` + `pg_trgm` fuzzy fallback |
| Cross-category discovery | Category filter (one at a time) | Same job in many categories not deduped in UX | Aggregate view + facet counts across categories |
| Filter by salary / remote / seniority | Not stored | Fields don't exist | Rules-based enrichment → structured columns + facets |
| Know what's fresh / not expired | `EndDate`, `LastSeenAt` exist but unused in UX | No "active" flag, no "new since visit" | Derived `IsActive`, indexed timestamps, "New" badge |
| Get notified of matches | None | No persistence of intent, no delivery channel | Saved searches + Telegram/email alert worker |
| Use it on a phone | Ops console, desktop-first | No mobile-first seeker UI | New public app, mobile-first search/detail screens |
| Trust / legal clarity | Scraped, no attribution shown | No visible source credit or link-back | "View on jobs.ge" links, attribution footer, rate-limit discipline |
| Beat jobs.ge meaningfully | Read-only mirror | Little additive value today | Bundle: description search + facets + freshness + alerts |

---

## 3. Roadmap (4 phases)

### Phase 1 — Search what's in the job
**Goal (user-facing):** A public search box that finds jobs by description text and returns ranked results with salary/remote/seniority visible on the card.

- **Features:** description full-text search; ranked results; 6 structured fields extracted and displayed; public search page + result cards; "View on jobs.ge" attribution.
- **Technical topics to learn:**
  - **PostgreSQL FTS (`tsvector`, GIN, `ts_rank`, `websearch_to_tsquery`).** *What:* an inverted index over lexemes. *Why architecturally:* keeps search in the database you already run — no new infra, transactional consistency with scrape writes, and you learn how relevance ranking and index maintenance actually work.
  - **`pg_trgm` trigram indexes.** *What/why:* Georgian has no Postgres stemmer, so `simple` config only tokenizes; trigrams give substring + typo tolerance across scripts without a language dictionary.
  - **Deterministic extraction pipelines (HtmlAgilityPack + regex + keyword dictionaries).** *Why:* teaches idempotent, versioned parsing you can re-run safely.
- **Backend/frontend/DB changes:** new `GET /api/search`; enrichment step in detail-parse; new nullable columns + generated `tsvector` column + GIN/trgm indexes; new public React routes.
- **Dependencies:** none (builds on current scrape).
- **Success metrics:** description queries return relevant results in <150ms p95; ≥70% of active jobs have ≥3 structured fields populated; public search page live.
- **Risks & mitigations:** *regex brittleness* → version the extractor, log low-confidence extractions, keep raw text; *HTML drift* → snapshot raw detail HTML for re-parse; *Georgian ranking oddities* → default to `simple` config + trigram fallback, don't over-tune.

### Phase 2 — Freshness & faceted filters
**Goal:** Filter results by salary/remote/type/seniority/language, hide expired jobs, and see what's new since last visit.

- **Features:** facet filters with live counts; active/expired derivation; "new since last visit" badge; saved searches via anonymous token (shareable URL, no login).
- **Technical topics to learn:**
  - **Faceted queries & count aggregation.** *Why:* teaches `GROUP BY`/`FILTER`, index selection, and the cost of computing facet counts on every query.
  - **Materialized views vs indexed timestamp queries.** *Why:* decide when precomputation beats a well-indexed live query at this data size (spoiler: usually the live query wins at thousands of rows — learn *why*).
  - **Anonymous identity via opaque tokens.** *Why:* lets you persist user intent without building auth — a real architectural pattern (capability URLs).
- **Backend/frontend/DB changes:** query params for facets; `saved_searches` table keyed by token; `IsActive` computed column/index; frontend filter panel + "new" logic using localStorage last-visit timestamp.
- **Dependencies:** Phase 1 structured fields + search endpoint.
- **Success metrics:** facet filtering used in >30% of sessions; expired jobs excluded by default; saved-search URLs created and revisited.
- **Risks:** *facet count cost* → cache counts per query signature; *token leakage* → tokens are unguessable but read-mostly, no sensitive data.

### Phase 3 — Alerts
**Goal:** Users get notified when new jobs match a saved search.

- **Features:** Telegram bot (`/subscribe`, `/mysearches`, `/stop`); email digest fallback; dedup so no job is sent twice; per-search frequency (instant vs daily).
- **Technical topics to learn:**
  - **Background job scheduling: `System.Threading.Channels` vs Hangfire.** *Why:* you already have a `BackgroundService`; learn when a queue/Channel pipeline suffices vs when you need Hangfire's persistence, retries, and dashboard.
  - **Idempotent notification / dedup design.** *Why:* exactly-once-ish delivery via a `notified` ledger keyed by (search, job) — a genuinely hard distributed-systems micro-problem.
  - **Bot/webhook integration & outbound providers.** *Why:* Telegram gives identity + delivery with zero auth build; email teaches deliverability (SPF/DKIM, provider APIs).
- **Backend/frontend/DB changes:** `alert_subscriptions`, `alert_deliveries` tables; matching worker; Telegram webhook endpoint; optional email provider (Resend/Postmark/SES).
- **Dependencies:** Phase 2 saved searches (the "intent" store) and Phase 1 search matching.
- **Success metrics:** subscriptions created; alert → click-through rate; zero duplicate sends in logs.
- **Risks:** *notification spam* → per-search caps + digest batching; *matching drift after re-enrichment* → match on stable job id, not on recomputed fields.

### Phase 4 (optional / future) — Semantic search & analytics
**Goal:** "Jobs like this," salary/market trends, semantic ("backend engineer" matches "server-side developer").

- **Features:** `pgvector` embeddings for similarity + semantic query; market dashboards (salary by category over time using your `scrape_runs` history).
- **Technical topics to learn:** **pgvector + multilingual embeddings** (handles Georgian+English in one vector space), approximate nearest neighbour (HNSW/IVFFlat), hybrid search (combine FTS score + vector score). *Why architecturally:* teaches embedding pipelines, index tradeoffs, and hybrid ranking — the highest-depth learning in the plan.
- **Dependencies:** Phases 1–2; a batch embedding step in the scrape pipeline.
- **Success metrics:** semantic queries surface results FTS misses; "similar jobs" clicked.
- **Risks:** *embedding cost/latency* → batch, cache, only embed active jobs; *over-engineering* → gate behind proven demand.

---

## 4. Deep-dive: search & discovery strategy

| Approach | Pros | Cons | When to use | Learning value |
|---|---|---|---|---|
| **ILIKE (current)** | Trivial; works now | No ranking, no descriptions, `%q%` can't use btree index, no multi-word logic | Tiny data, title-only prototypes | Low |
| **Postgres FTS (tsvector/GIN)** | No new infra; transactional with writes; ranking (`ts_rank`); `websearch_to_tsquery` handles multi-word AND/OR/phrase; fast at this scale | No stemming for Georgian (`simple` only tokenizes); tuning ranking is fiddly | **Your primary path** — thousands of rows, mixed language, description search | **High** — inverted indexes, relevance, index maintenance |
| **pgvector / embeddings** | Semantic match across languages; "similar jobs"; hybrid with FTS | Needs embedding pipeline + model; ANN index tuning; cost/latency | Future upgrade once FTS proven | **Very high** — embeddings, ANN, hybrid ranking |
| **External (Meilisearch/Typesense/OpenSearch)** | Typo tolerance + facets out of the box; great DX | New service to run/deploy/sync; another source of truth; overkill at this scale | If facets+typo become a bottleneck and you want managed relevance | Medium — sync pipelines, but less *DB* depth |

**Recommendation.** **Primary (Phase 1–2): Postgres FTS with `simple` config + `pg_trgm`.** Use a stored generated `tsvector` column combining weighted title (`A`), company (`B`), description (`C`); a GIN index on it; and a separate `pg_trgm` GIN index on title/company for fuzzy fallback. Parse user input with `websearch_to_tsquery` so multi-word queries "just work" (`senior backend remote` → AND semantics, quotes → phrases). For Georgian: `simple` tokenizes on whitespace/punctuation, which is correct since there's no built-in Georgian dictionary; trigrams cover partial matches and typos across both scripts. This searches descriptions, handles multi-word, and needs zero new infrastructure.

**Future upgrade path: `pgvector`** for semantic/multilingual matching, combined with FTS in a hybrid score. It stays inside Postgres (no new service), teaches the most, and directly serves Georgian↔English concept matching that neither ILIKE nor `simple` FTS can. Meilisearch is the pragmatic alternative *only* if you decide managed typo-tolerant facets matter more than learning depth — but for a learning-first solo project, keep it in Postgres.

---

## 5. Data enrichment plan

**Fields to extract (priority order):**

1. **Salary** — `salary_min`, `salary_max`, `salary_currency`, `salary_period` (month/hour/year). Regex over GEL/₾/USD/EUR patterns and ranges.
2. **Location** — `city` (Tbilisi/Batumi/Kutaisi/… + Georgian spellings) via a controlled gazetteer dictionary.
3. **Work mode** — `remote | hybrid | onsite`, keyword dictionary bilingual (e.g. *დისტანციური*, "remote", "hybrid").
4. **Employment type** — `full_time | part_time | contract | internship` (bilingual keywords).
5. **Seniority** — `intern | junior | mid | senior | lead` from title + description cues.
6. **Language requirement** — `en | ka | ru | de …` from "fluent English", *ინგლისური* etc.
7. **Skills/tags** (later) — many-to-many `job_tags` from a curated skill dictionary (.NET, React, SQL…).

**How.** Start **rules/regex + bilingual keyword dictionaries** — deterministic, debuggable, free, and the right *learning* artifact (a versioned extraction pipeline). Add an **optional LLM batch pass** only for the messy long tail (e.g. salary phrased in prose), run offline over stored text, results written back with a `confidence` and `source='llm'`. Never call an LLM in the request path.

**Schema.** Add the six scalar fields as **nullable columns on `jobs`** (low cardinality, needed in filters/facets — column storage keeps facet queries simple and indexable). Skills go in a separate `job_tags` (m2m). Add `enrichment_version` (int) and `enriched_at` on `jobs`, and a `description_html` column so future extractors can use structure without re-scraping.

**Backfill.** Idempotent maintenance worker/endpoint that selects rows where `enrichment_version < CURRENT_VERSION`, re-runs the extractor over stored `Description`/`description_html`, and updates fields + version. No re-scrape needed for text-derivable fields (respects rate limits). When you bump the extractor, bump the version constant and the backfill naturally reprocesses. This teaches versioned, resumable batch processing.

---

## 6. UX architecture

**Split public vs ops — but keep ONE React app.** Two deployables doubles build/CI/deploy config and duplicates the API client and TS types for a solo dev. Instead, use **two route groups with two layouts** in the existing Vite app:

```
/                    PublicLayout  — search home
/search              PublicLayout  — results + facets
/jobs/:id            PublicLayout  — job detail (public-facing)
/saved/:token        PublicLayout  — a saved search / alert manager
/ops                 OpsLayout     — existing scrape dashboard
/ops/runs            OpsLayout
/ops/batches/:id     OpsLayout
/ops/categories      OpsLayout
```

Public routes are mobile-first, minimal chrome, no scrape jargon. Ops routes keep today's console (later guarded by a simple shared-secret or basic auth). If ops ever needs separate auth/deploy, extract it then — not now.

**Key seeker screens:**
- **Home:** big search box, category chips, "N new today" freshness banner, trending/newest jobs.
- **Results:** left/collapsible facet panel (salary, remote, type, seniority, language, category), ranked cards showing title, company, salary, remote badge, "new" badge, posted/expiry, "View on jobs.ge".
- **Job detail:** full description, all structured fields as chips, prominent link-back to the original jobs.ge posting, "similar jobs" (Phase 4).
- **Saved search / alerts:** the query + filters that define a search, a shareable token URL, and (Phase 3) "get alerts via Telegram/email".

---

## 7. Alerts & notifications

| Channel | Pros | Cons | Auth needed? |
|---|---|---|---|
| **Telegram bot** | Huge in Georgia; identity via `chat_id` (no login to build); instant; free | Requires bot setup + webhook | **No** (chat_id *is* the identity) |
| Email digest | Universal; good for daily batches | Deliverability (SPF/DKIM), provider cost | Email = identity (magic link) |
| RSS | Zero auth; trivial | Passive, low engagement | No |
| Browser push | Native-ish | Service worker + permissions friction, poor retention | Subscription token |
| Webhook | Power users/integrations | Niche | Token |

**Default: Telegram bot (Phase 3), email digest as secondary.** Telegram sidesteps the entire auth problem — the `chat_id` from the webhook *is* the stable user identity, so saved searches bind to it directly. That's the single biggest reason to pick it for this audience.

**Architecture.**
- `saved_searches` (from Phase 2) holds the query + filters.
- `alert_subscriptions` links a `saved_search` → channel (`telegram:chat_id` or `email`) + frequency (`instant | daily`).
- `alert_deliveries` is the **dedup ledger**: one row per (subscription, job_id); matching worker only sends jobs not already in the ledger.
- A worker (start with a **Channels**-based pipeline off your existing `BackgroundService`; graduate to **Hangfire** if you need persistence/retries/dashboard) runs after each scrape: for each active subscription, run its stored query, diff against the ledger, send new matches, record deliveries.

**What requires auth:** nothing for Telegram. Email needs lightweight verification (magic link) to prevent subscribing someone else's address. Ops console should get a shared secret before any public deployment.

---

## 8. Learning curriculum map

| Feature | Technical topic | Concepts to study | Artifact produced |
|---|---|---|---|
| Search descriptions | Postgres FTS | `tsvector`, GIN, `ts_rank`, weights, `websearch_to_tsquery`, `simple` config | `/api/search` + `EXPLAIN (ANALYZE)` writeup |
| Fuzzy/typo & Georgian | `pg_trgm` | Trigram similarity, GIN vs GiST, `similarity()` thresholds | Trigram index + fallback query |
| Salary/location/remote fields | Extraction pipeline | Regex, HtmlAgilityPack XPath, bilingual keyword dictionaries, confidence scoring | Versioned `EnrichmentService` + tests |
| Backfill existing rows | Idempotent batch processing | Versioned processors, resumable jobs, chunked updates | Backfill worker + `enrichment_version` |
| "New since visit" | Indexed timestamps vs matviews | Index selection, when precompute loses at small scale | Benchmarked query + "New" badge |
| Faceted filters | Aggregation queries | `GROUP BY … FILTER`, count caching, query-signature keys | Facet endpoint + count cache |
| Saved searches | Capability URLs | Opaque tokens, read-mostly persistence | `saved_searches` + shareable link |
| Alerts | Scheduling + dedup | Channels vs Hangfire, exactly-once ledgers, idempotency | Matching worker + `alert_deliveries` |
| Telegram bot | Webhooks/bot APIs | Long-poll vs webhook, chat_id identity, command routing | Bot webhook endpoint |
| Email digest | Deliverability | SPF/DKIM, provider APIs, batching | Digest job + template |
| Semantic search (later) | pgvector | Embeddings, HNSW/IVFFlat, hybrid ranking | Vector column + hybrid scorer |
| Public vs ops UI | Frontend architecture | Route groups, layout composition, shared API client | Two-layout SPA |

---

## 9. Anti-patterns & scope traps

**Do NOT build (or defer hard):**
- **Full user accounts / OAuth in Phase 1–2.** Anonymous tokens + Telegram chat_id cover intent and alerts without it. Auth is a tar pit.
- **A second scraper for other job sites.** Doubles maintenance, dilutes the product, adds legal surface. Master jobs.ge first.
- **External search engine (Meilisearch/OpenSearch) before FTS is proven insufficient.** New infra, sync bugs, less learning. Postgres handles thousands of rows easily.
- **Real-time WebSocket job feed.** Hourly scrape → polling/badges are plenty. No live infra.
- **Microservices / k8s / message brokers.** One process, one Postgres. The single-scraper constraint you already have is *fine*; don't build leader election.
- **Native mobile app.** Mobile-first web covers it.
- **Custom ML training / a recommendation engine.** pgvector similarity (later) is the ceiling; don't train models.
- **Over-normalized enrichment schema** (a generic EAV `attributes` table). Concrete columns for the six known fields are simpler to query and index.
- **GraphQL.** REST endpoints you have are fine at this size.
- **Premature LLM-in-request-path enrichment.** Batch, offline, cached — never per request.

**Meta-trap:** treating "structured extraction" as a solved static thing. HTML *will* drift. Version the extractor, keep raw text/HTML, log low-confidence output. That discipline is the actual learning.

---

## 10. Recommended Phase 1 plan (actionable tickets)

**Phase goal:** public, ranked, description-level search with salary/remote/seniority visible.

| # | Title | User story | Acceptance criteria | Files/areas | Effort |
|---|---|---|---|---|---|
| 1 | Store raw detail HTML + tsvector column | — (enabler) | `jobs` gains `description_html`, `search_vector` (generated: title `A`, company `B`, description `C`); GIN index created via migration; scrape writes both | EF migration, `Job.cs`, `HtmlProcessor.cs`, `Repo.cs` | M |
| 2 | pg_trgm fuzzy index | As a user I get results despite typos | `pg_trgm` extension + GIN index on title/company; `similarity()` threshold tuned; documented | migration, `Repo.cs` | S |
| 3 | Search endpoint | As a seeker I search job *text* and get ranked results | `GET /api/search?q&page&pageSize&…`; uses `websearch_to_tsquery('simple', q)` + `ts_rank`; trigram fallback when FTS empty; returns cards incl. structured fields; p95 <150ms | new `SearchController`/endpoint, `Repo.cs` | M |
| 4 | Enrichment service (rules) | — (enabler) | `EnrichmentService` extracts salary/location/remote/type/seniority/language from text; bilingual keyword dicts; unit tests on real samples; writes fields + `enrichment_version` | new `EnrichmentService.cs`, `HtmlProcessor.cs`, `Job.cs` | L |
| 5 | Enrichment columns + backfill | As a user I see salary/remote on cards | migration adds 6 nullable columns + `enrichment_version`/`enriched_at`; backfill endpoint/worker reprocesses rows where version stale; ≥70% active jobs get ≥3 fields | migration, backfill worker, `Repo.cs` | M |
| 6 | Public app shell + layouts | As a seeker I land on a search-first page | Route groups: `PublicLayout` (`/`, `/search`, `/jobs/:id`) vs `OpsLayout` (`/ops/*`); existing console moved under `/ops`; mobile-first | `frontend/` routing, layouts, nav | M |
| 7 | Results + job detail screens | As a seeker I browse ranked cards and open details | Search page with query box + ranked cards (title, company, salary, remote/new badges, expiry); detail page with structured chips + prominent "View on jobs.ge" | `frontend/` pages, TanStack Query hooks | M |
| 8 | Attribution & rate-limit hygiene pass | As jobs.ge I'm credited and not hammered | Every job links back to source; footer attribution; confirm throttle/delay config respected; brief NOTICE on data source in README | frontend components, README, config check | S |

---

## 11. Open questions for the owner

1. **UI language:** Georgian-only, English-only, or bilingual toggle? (Affects enrichment dictionaries and all copy.)
2. **Public deployment intent:** Is this going live for real users, or staying a portfolio/learning artifact? (Changes the auth/legal bar.)
3. **Auth appetite:** Comfortable relying on anonymous tokens + Telegram `chat_id`, or do you eventually want real accounts?
4. **Monetization stance:** none / donations / non-intrusive ads? (Even "none" is a decision that affects legal posture and hosting.)
5. **Legal comfort with republishing descriptions:** Full text with attribution, or snippet + link-back only? Have you checked jobs.ge ToS/robots.txt?
6. **Alert channels:** Is Telegram-first acceptable for your audience, or is email non-negotiable from day one?
7. **Salary reality:** How often does jobs.ge actually list salary? (If rare, deprioritize salary facet and lead with remote/seniority.)
8. **Hosting model:** single host (API serves built React) or split deploy + CORS? (Simplest is single host for the seeker app.)
9. **Data retention:** Keep expired jobs forever (enables historical/market analytics in Phase 4) or prune?
10. **Semantic search ambition:** Is pgvector/embeddings a "definitely want to learn this" or "nice-to-have"? (Determines whether Phase 4 gets scheduled or shelved.)

---

### Legal / ethical note (brief but explicit)
Scraping here is HTML parsing of a third party, so: **always link back** to the original jobs.ge posting, **show visible attribution** for the data source, **respect rate limits** (keep your existing throttle/delay config, honor robots.txt), avoid re-publishing full descriptions if ToS restricts it (snippet + link-back is the safe default), and keep a single scraping instance as today. Treat freshness/aggregation/alerts — not verbatim mirroring — as the value you add.
