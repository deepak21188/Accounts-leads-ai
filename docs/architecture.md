# Architecture and design decisions

This document describes how the system is put together and why. It is written for a reviewer
who wants to understand the engineering, not just read a feature list — see the [README](../README.md)
for the high-level pitch and the [setup guide](setup.md) for running it.

## System overview

A public React form captures a prospective client's inquiry. The API persists it and hands off
to a background Worker, which calls an AI model to extract structured facts from free-text
input, then applies deterministic business rules to score and prioritize the lead. An
authenticated accountant reviews the queue through a separate, Entra-protected part of the same
application. Nothing is sent back to a client automatically — a human always reviews AI output
before it reaches anyone outside the firm.

The system is intentionally a **modular monolith with two execution hosts** (an API and a
Worker), not a set of independently deployed services. At this scale, a lead-generation tool
for one firm, network-boundary microservices would add operational cost without a matching
benefit; the outbox/queue boundary already gives the one place that genuinely needs async
decoupling — AI processing — its own failure domain, without paying for service-to-service
auth, versioned contracts, or distributed tracing across a dozen deployables.

## Decisions at a glance

| Area | Decision | Why |
| --- | --- | --- |
| Modularity | Modular monolith, two hosts (API, Worker) | Async decoupling where it matters (AI calls) without microservices overhead |
| Reliability | Transactional Outbox | Lead write and event publication can't silently diverge |
| Delivery guarantee | At-least-once, idempotent consumer | Message brokers don't offer exactly-once; the consumer must tolerate replays |
| Retry policy | 3 broker delivery attempts, then dead-letter | Bounded automatic recovery; permanent failures become visible, not silently dropped |
| AI integration | Behind an application-defined interface | Swappable provider, testable orchestration without a live model call |
| Qualification | Deterministic rules over AI-extracted facts | Scoring must be explainable and reproducible; the model proposes facts, not a verdict |
| Validation | Two-tier: request shape (API) + business rules (Application) | Rules hold regardless of caller — HTTP, Worker, tests, future integrations |
| Repository pattern | Not used — `DbContext` via an interface | One well-understood abstraction (EF Core) instead of two overlapping ones |
| Orchestration framework | No MediatR | Direct handler calls are enough at this command/query count; a mediator adds indirection with no matching payoff yet |
| Authentication | Microsoft Entra ID, single tenant, explicit assignment | Small, known accountant population — no self-service signup, no multi-tenant isolation to build |
| Frontend state | TanStack Query, no global store | Server state (leads) dominates; there's little client-only state to justify Redux/Zustand |
| Hosting | Static Web Apps + App Service (no container) + Container Apps (Worker, containerized) | Container only where a long-running singleton process needs it; everything else stays platform-managed |

## Project structure and boundaries

```text
server/src/
├── AccountingLeads.Api/            HTTP boundary: controllers, auth wiring, DI composition
├── AccountingLeads.Worker/         Background host: broker listener, outbox publisher
├── AccountingLeads.Application/    Use cases, orchestration, qualification rules, interfaces
├── AccountingLeads.Domain/         Entities, value objects, invariants — zero external deps
└── AccountingLeads.Infrastructure/ EF Core, AI SDK adapter, messaging adapter
```

Dependency direction is one-way and inward: `Api` / `Worker` depend on `Application` and
`Infrastructure`; `Infrastructure` depends on `Application` and `Domain` to implement their
interfaces; `Domain` depends on nothing outside itself — no ASP.NET Core, no EF Core, no Azure
SDKs. This is what makes the domain model unit-testable with zero test-double setup and keeps
business rules from leaking into a controller or a message handler.

Two hosts compose the shared layers independently: capturing a lead never requires the AI
provider or the message broker to be reachable, and processing a lead never spins up the HTTP
pipeline. Each host's dependency-injection registration is split so that, for example, the API's
container never sees Service-Bus-specific services — ASP.NET Core validates the *entire*
registered dependency graph at startup regardless of what's actually resolved, so a
Worker-only interface registered where the API can see it would break API startup the moment
that interface's own dependencies weren't also registered there.

No generic `IRepository<T>` sits in front of EF Core. `IApplicationDbContext` — a narrow
interface exposing only the `DbSet`s and query methods each use case actually needs — is used
directly. A repository layer here would just be a second abstraction wrapping the first one EF
Core already provides, for no real swap-target (there is no plan to leave SQL Server).

## Key architectural patterns

### Transactional Outbox

The API writes the new lead row and its corresponding outbox event row in **one database
transaction** — they succeed or fail together, so a successful `201` response is a real
guarantee that publication is at least scheduled, not a race against a separate message send
that might silently never happen. A background publisher (in the Worker) polls unpublished
outbox rows on a timer, sends each to the message broker, and marks it published only after the
send succeeds. If the process crashes between a successful send and marking the row published,
the same row is retried and sent again — the tradeoff is a possible duplicate delivery, which is
why the consumer has to be idempotent (see below) rather than the publisher trying to guarantee
exactly-once, which no combination of a database and a message broker can actually provide
atomically across both systems.

Failed sends back off exponentially (starting at 30 seconds, capped at 30 minutes) so one
persistently failing row can't monopolize the publisher's attention or spam the broker, while
still recovering automatically once the underlying issue clears.

### Idempotent processing

Because delivery is at-least-once, the same event can arrive twice. Before doing any real work,
the consumer checks whether the referenced lead has already reached a terminal processing state
and short-circuits if so — no duplicate AI call, no duplicate qualification result, no
double-charged API usage. Idempotency is checked against durable state (the lead's own
processing status), not against the message itself, since a naive "have I seen this message ID"
check would need its own separate storage and still wouldn't help if the *first* attempt had
already completed its side effects before crashing.

### Retry and dead-letter

The broker is configured for a bounded number of delivery attempts (3) before a message
dead-letters instead of retrying forever. Application code does not stack its own uncoordinated
retry loop on top of that — two independent retry mechanisms disagreeing about when to give up
is a worse failure mode than one well-understood one. When a message exhausts its attempts, the
lead's processing status reflects the failure explicitly, so an accountant reviewing the queue
sees "processing failed" rather than a lead that's silently stuck in "pending" forever with no
visible explanation.

### AI abstraction

The Application layer never references an AI SDK type directly — it depends only on an
interface describing the shape of the extraction it needs (structured facts in, from free text).
Infrastructure implements that interface against the actual provider. This keeps orchestration
logic (idempotency checks, saving results, triggering qualification) testable with a hand-written
fake instead of a live model call, and means swapping providers is a Infrastructure-layer change,
not a rewrite of the use case.

### Two-tier validation

Request-shape validation (required fields, formats, ranges) happens at the API boundary.
Business-rule validation (valid state transitions, domain-specific constraints) happens in the
Application/Domain layers, independent of *how* a request arrived. The distinction matters
because the Worker also drives Domain state transitions (marking a lead processed, qualified) —
if business rules only lived in an API filter, the Worker's path would be unprotected by them.

### Deterministic qualification

The AI's job stops at extracting facts from unstructured text — requested services, approximate
revenue, urgency signals, a completeness read of what the inquiry actually said. A separate,
plain deterministic function then turns those facts into a score and priority using fixed,
published rules (see below). The model is never asked to output a score or a priority directly.
This is a deliberate control: a scoring rule can be explained, audited, and versioned in a way
that "the model decided" cannot, and it means the same facts always produce the same score,
regardless of model sampling variance.

## Reliable messaging — full flow

1. API validates the request, then in one transaction: inserts the `Lead` row and an outbox row
   containing a serialized event envelope (event ID, event type, schema version, timestamp, and
   the lead ID as payload).
2. The Worker's outbox publisher polls eligible rows, sends each to the broker with the outbox
   row's own ID reused as the broker message ID (so the *envelope's* identity, not a new random
   one, is what downstream dedup would key on), then marks the row published.
3. The Worker's consumer receives the message, validates its type and schema version, and opens
   a fresh unit-of-work per message (not a shared, long-lived scope — a defect in handling one
   message must not corrupt state for the next one processed in the same host lifetime).
4. It loads the lead, checks whether processing already completed (idempotency), and if not,
   calls the AI abstraction, persists the extracted analysis, runs qualification, and saves the
   final status — all before the message is marked complete on the broker. If anything after the
   AI call fails, the message is not completed, so a redelivery retries the whole unit rather
   than leaving a lead half-processed.
5. Exhausted retries land the message in the broker's dead-letter destination, and the lead's own
   status is what actually surfaces the failure to an accountant — the dead-letter destination is
   an operational recovery mechanism, not the user-facing signal.

## Qualification scoring model

Score is a sum across five weighted dimensions, each contributing up to a fixed maximum:

| Dimension | Max points | What it captures |
| --- | --- | --- |
| Service fit / value | 30 | Whether the requested service matches what the firm actually offers, and its relative value |
| Annual revenue | 25 | Approximate client size, as stated or extracted |
| Urgency / deadline | 25 | How soon the client needs a response — explicit deadlines take priority over inferred urgency language |
| Catch-up work / scope | 15 | Backlog signals (e.g. months of unfiled bookkeeping) that indicate scope beyond a routine engagement |
| Information completeness | 5 | Whether the inquiry gave enough detail to act on without back-and-forth |

Total score maps to priority: **0–39 Low, 40–69 Medium, 70–100 High.** An inquiry for a service
the firm doesn't offer is capped at 39 regardless of the other dimensions' points — an
unsupported-service lead should never rank as urgent purely because it happened to be large or
time-sensitive. Deadline extraction anchors relative phrasing ("by next month") to the lead's own
submission time, and a deadline that's already overdue is treated as valid — and scores in the
highest urgency band — rather than being rejected as impossible input, since real inquiries do
arrive already late. Every score is stored alongside the specific reasons that produced it and
the rules-version applied, so a given result stays explainable and reproducible even if the
rules are revised later — the qualification rules document is versioned independently for
exactly that reason: a future change bumps to a new version rather than silently rewriting the
meaning of an already-computed score.

## Authentication design

Accountant sign-in uses Microsoft Entra ID against a single tenant — this is a firm-internal
tool for a small, known population of employees, not a multi-tenant or self-service product, so
there is no signup flow and no per-organization isolation to design for. Two app registrations
exist: a public-client SPA registration and a protected-resource API registration exposing one
delegated scope. The frontend uses MSAL for the browser to run the Authorization Code flow with
PKCE and caches tokens in `sessionStorage` (cleared on tab close, unlike `localStorage`, which
matters more for a shared/kiosk-adjacent office machine than a purely personal one). The API
validates the bearer token's signature, issuer, audience, and required scope on every
authenticated request.

Tenant membership alone does not grant access — both the SPA and API app registrations require
explicit user assignment, so adding someone to the Entra tenant does not, by itself, let them
reach the tool; an administrator has to separately approve them against these two
applications. The public lead-capture endpoint stays anonymous by design (prospective clients
are not Entra users); every accountant-facing endpoint requires both a valid token and the
delegated scope together — a token that's merely valid but missing the scope must still be
rejected, not treated as "authenticated enough."

Managed Identity, used for the Worker's and API's own access to Azure resources (the database,
the message broker, the AI service), is a completely separate authentication concern from
accountant sign-in — one is workload identity for the application talking to Azure, the other is
a human proving who they are to the application. Neither the SPA nor the API needs a stored
client secret for the accountant sign-in flow itself.

On the frontend, session handling treats a few distinct states as genuinely different rather
than collapsing them into one generic "error": a token that's expired or was never acquired
triggers one bounded, automatic re-authentication attempt (not an unbounded redirect loop if
re-authentication itself keeps failing); a token that's valid but lacks authorization renders an
explicit "not authorized" state instead of behaving like a network failure; and a query that
resolves to a legitimate empty result (nothing found) is distinguished from a query that never
completed — conflating "no data" with "still loading" or "failed" produces a UI that either lies
about a real result or spins forever on a legitimate one.

## Delivery strategy: vertical slices

The system was built in thin, end-to-end slices — each one runs and is verifiable on its own
before the next begins, rather than building all of one layer (all domain, then all
infrastructure, then all UI) before anything is demonstrable:

1. **Lead capture** — form through to a durable, atomically-committed lead + outbox row.
2. **Message publication** — the outbox reliably reaches the message broker.
3. **AI processing** — the broker message drives a real extraction call and a persisted result.
4. **Qualification** — deterministic scoring over the extracted facts.
5. **Accountant access** — authentication, then a real dashboard (list, filter, search,
   pagination, detail) over the qualified leads.
6. **Response review** *(not built in this snapshot)* — AI-assisted response drafting with
   mandatory human review before anything reaches a client.

Each slice intentionally touches every layer it needs (Domain through UI or Domain through
Worker) rather than stopping at "the backend is done." This keeps integration risk visible early
— a broken assumption between, say, the outbox schema and the Worker's deserialization shows up
in slice 2, not after four more slices have been built on top of an untested assumption.

## Notable engineering challenges

A few problems that took real debugging to track down, kept here because they're the kind of
thing that doesn't show up just from reading a feature list:

- **A scope-only authorization attribute silently no-opped.** The framework's "require this
  delegated scope" attribute does nothing on its own — it has to be paired with a separate
  "require authentication" attribute, or an endpoint marked as scope-restricted is actually wide
  open. This was caught by a failing authorization test, not by inspection, which is itself the
  argument for testing the 401/403 boundary explicitly rather than trusting that an attribute's
  name describes its full behavior.
- **A credential-resolution chain that aborts on the first hard failure, not just the first
  miss.** The default credential chain used for passwordless Azure authentication is supposed to
  fall through to the next credential source when one doesn't apply — but on a non-Azure dev
  machine, one of the sources fails in a way that's classified as an outright error rather than
  "not available here," which aborts the whole chain before it reaches the source that would
  have worked. The fix was excluding that specific source from the chain in local development
  while keeping it for the actual Azure-hosted environment, where it's the correct and only
  source.
- **A sign-in redirect that silently returned the user to the login screen even on success.**
  The auth library's post-redirect navigation defaults to returning the browser to wherever the
  sign-in was initiated from — which was the login page itself — regardless of whether sign-in
  succeeded. The login page never checked whether the user was now actually authenticated, so a
  successful sign-in visually looked identical to a failed one: it just bounced back to the same
  screen. The fix was an explicit authenticated-state check on that page driving a redirect
  forward, not relying on the library's default landing behavior.
- **A "did this succeed" check that worked by accident until a legitimately empty result
  appeared.** Early session-recovery logic checked "do we have data back" as a truthy check to
  decide a request had succeeded. That happened to work while every query's success value was
  non-empty, but broke the moment a second query was added whose correct, successful result
  *is* an empty value (a lookup that legitimately finds nothing) — a falsy success was
  indistinguishable from a failure. The fix was switching to the data-fetching library's own
  explicit success flag instead of inferring success from the shape of the data.
- **A packaging step that broke the deployment target without breaking the build.** A
  zip-based deployment step produced an archive that built and looked correct locally, but
  silently failed to unpack correctly on the Linux-based hosting target — the archive tool used
  the host OS's path-separator convention internally, which the target's unpacking step didn't
  handle. Nothing in the build pipeline flagged this; it only showed up as missing files at
  runtime, which is the uncomfortable class of bug a packaging step should be tested against
  directly rather than assumed correct because "the build succeeded."

## Limits and what's actually verified

The Worker is constrained to a single running instance, including during rollouts — the outbox
publisher and message consumer are not designed for concurrent multi-instance coordination, so
this is a deliberate scaling ceiling for this snapshot, not an oversight.

This source snapshot's own local verification (see [validation.md](validation.md)) exercises
unit tests, a frontend production build, and lint — it does not re-run SQL integration tests or
a live Entra/Azure round trip, since those require real provisioned infrastructure this snapshot
doesn't carry credentials for. In the original project, integration tests exercise real SQL
persistence and the full HTTP authorization boundary using a test-only authentication scheme
(not live Entra token validation, which was instead verified manually against a real tenant),
and the full flow — lead capture through AI processing through qualification through accountant
sign-in — was verified end-to-end against live Azure infrastructure. That live verification
history isn't reproducible from this standalone snapshot alone.

Response drafting, review, and approval (slice 6) are not implemented in this snapshot — the
dashboard is read-only: list, filter, search, paginate, and view detail. A low qualification
score means low priority for follow-up, not automatic rejection; every lead, regardless of
score, remains visible and reviewable by an accountant.
