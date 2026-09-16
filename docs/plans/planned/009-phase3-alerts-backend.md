# 009 - Phase 3 alerts backend

**Status:** In progress (slices 1 to 6 shipped; SMS provider and hardening remain)

Implements [`../../roadmap.md`](../../roadmap.md) Phase 3, with two deliberate departures from the roadmap sketch:

- **Channels are email and SMS, not Telegram.** Telegram remains a later drop-in behind the same abstraction.
- **Alerts bring their own filter store.** Phase 2 `saved_searches` never shipped, so `alert_subscriptions` holds the criteria directly.

## Goal

A passive job seeker registers a filter once against a verified contact and is notified when a new matching job appears. Alert work must never block or slow the scrape loop.

## Constraints

1. Alert processing is fully decoupled from fetching.
2. The channel is swappable without touching matching logic.
3. Only the person who requested a filter gets its alerts, and their address is proven.
4. No user accounts, no passwords, no ASP.NET Identity.

## Architecture

The scrape path writes a `job_events` row in the *same* `SaveChangesAsync` that persists the enriched job (`Data/Repo.cs`, `UpsertDescriptionAsync`). That single INSERT is the only coupling. Three background workers do the rest:

| Worker | Runner | Interval | Responsibility |
|--------|--------|----------|----------------|
| `AlertOutboxWorker` | `AlertOutboxDrainer` | 30s | claim events, reverse match, insert deliveries, enqueue instant messages |
| `AlertDigestWorker` | `AlertDigestBuilder` | 5 min | find due digests by subscriber local time, render, enqueue |
| `AlertSendWorker` | `AlertSendPump` | 10s | claim messages, send through a channel, record outcome or retry |

Each worker is a thin `PeriodicTimer` loop delegating to a singleton runner, mirroring `JobScrapeWorker` and `ScrapeBatchRunner`, so integration tests can drive one pass deterministically.

## Tables

| Table | Purpose |
|-------|---------|
| `job_events` | transactional outbox, drained with `FOR UPDATE SKIP LOCKED` |
| `alert_subscribers` | a verified (channel, address) pair, double opt in, capability token |
| `alert_subscriptions` | one saved filter plus cadence, criteria as typed columns and `text[]` arrays |
| `alert_deliveries` | dedup ledger and digest backlog in one |
| `alert_messages` | outbound queue with bodies rendered at enqueue |

Two constraints carry most of the correctness:

- **`alert_deliveries (SubscriptionId, JobId)` unique** is the entire no-duplicate-alerts guarantee. Enforced by Postgres, so no crash, race, replay or double drain can send the same job twice.
- **`alert_messages (IdempotencyKey)` unique**, together with `alert_subscriptions.LastDigestSentOn` (a local calendar date), makes a double digest impossible including across a DST transition.

Criteria use typed columns and native `text[]`, not JSONB: the field set is small and closed, it mirrors the `jobs` enrichment columns one for one, and the reverse match prefilter needs an indexed answer to "which subscriptions accept city X", which is a GIN containment lookup.

## Which event triggers matching

`JobReady` is emitted from `UpsertDescriptionAsync` when `DetailsFetchedAt` was null. That is the only correct point: the listing phase inserts a job whose description and every enrichment field are still null, so matching there would evaluate content criteria against nulls and systematically miss. `ApplyEnrichment` runs immediately before the save, so that instant is the first at which the row is coherent.

Supporting events: `JobCategoryLinked` when a complete job is later linked to another category by a parallel pass, and `JobReEnriched` when a complete job's description actually changes.

`BackfillEnrichmentAsync` emits nothing, deliberately. Bumping `EnrichmentService.CurrentVersion` would otherwise fan out one event per job and alert everyone about the whole corpus.

## Matching

One matching path, two delivery paths. Matching always runs *reverse* (which subscriptions match this one job), once per event, for both cadences, and always writes a delivery row. Cadence only decides whether a message is enqueued now or the delivery waits for the digest. The digest never re-runs filters, so a later re-enrichment cannot change what was already matched.

Reverse match is three steps: an indexed SQL prefilter over the array GINs, pure in-memory evaluation of substring and salary criteria, then one batched FTS round trip reusing the exact `websearch_to_tsquery('simple', ...)` semantics of `Repo.SearchWithFtsAsync`, so a filter matches in alerts exactly what it matches in `/api/search`. The trigram fallback is not used for alerts: it depends on the state of the whole corpus rather than on the job being evaluated.

Forward matching (`AlertCriteriaQueryBuilder`) exists only for `POST /api/alerts/preview`.

`MatchFromAt`, stamped at confirmation, is what stops a new subscription from alerting on the entire existing corpus.

## Slices

1. **Schema and outbox.** Entities, DbContext mappings, `AddAlerts` migration, `AlertOptions` and validator, the emit sites in `Repo.cs`. **Shipped.**
2. **Criteria model, matcher, query builder, validator, `POST /api/alerts/preview`, rate limiting.** **Shipped.**
3. **Subscribers, double opt in, channel and renderer registries, send pump, management endpoints.** **Shipped**, on the console channel.
4. **Instant delivery: outbox drainer, delivery ledger, reverse match SQL.** **Shipped.**
5. **Daily digest: builder, timezone scheduling, overflow handling, pause and resume.** **Shipped.**
6. **Real email: Postmark channel, `List-Unsubscribe` one-click headers, the `/a/c/{token}` and `/a/u/{token}` landing routes.** **Shipped**, unverified against live Postmark (no account yet).
7. SMS: a provider channel wired to the existing `SmsAlertRenderer` and E.164 normalization. Not started; the renderer, caps and cost guards already exist, so this is one class plus a registration. `Sms.Provider` other than `Console` fails at startup on purpose.
8. Hardening: dead letter ops endpoint, bounce and complaint webhook, retention prune, detail-less job sweeper.

Deviation from the original plan, made during slice 3: manage and unsubscribe tokens are **derived** by HMAC from the subscriber id plus `Alerts:TokenSecret` rather than randomly generated. Every message footer needs that link long after the subscriber was created, so a stored-hash-only random token could not be rebuilt at send time. Deriving it keeps the original intent (no usable token in the database) and removes the stored secret entirely; the row still holds a hash, used only to look the subscriber up. Confirmation tokens remain random and single use.

## Config keys

All under `Alerts`, disabled by default with console providers, so a fresh clone runs the whole opt in flow with no credentials. Provider API keys go in user secrets (`Alerts:Email:ApiKey`) or environment variables (`Alerts__Email__ApiKey`), never in `appsettings.json`.

## Notes

- No new NuGet packages. Rate limiting, `Base64Url`, `Guid.CreateVersion7()` and `TimeProvider` are all in box.
- The `AddAlerts` migration is purely additive. Nothing existing is altered.
- `ScrapePerformanceFixture.ResetScrapeDataAsync` truncates `jobs ... CASCADE`, which now also clears `job_events` and `alert_deliveries`. That is intended and needs no fixture change.
