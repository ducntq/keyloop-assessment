# System Design — Keyloop Unified Service Scheduler

**Scenario A: The Unified Service Scheduler (Ownership Domain)**

A resource-constrained automotive appointment scheduling engine. Every booking must
simultaneously reserve **two** scarce resources — a physical **service bay** and a
**certified technician** — for a contiguous interval, with zero tolerance for
double-booking under concurrency.

---

## 1. Component Architecture

```mermaid
flowchart TD
    Client["HTTP client<br/>(Swagger UI, requests.http, integration tests)"]

    subgraph Api["KeyloopScheduler.Api"]
        Corr["CorrelationIdMiddleware"]
        Ctrl["Controllers<br/>Appointments · Availability"]
        Filter["GlobalExceptionFilter<br/>RFC 7807 / problem+json"]
        Swagger["Swagger / OpenAPI 3.0"]
    end

    subgraph Infra["KeyloopScheduler.Infrastructure"]
        DI["DependencyInjection<br/>AddInfrastructure()"]
        Booking["AppointmentBookingService"]
        Avail["ResourceAvailabilityQuery"]
        Catalog["ResourceCatalogQuery"]
        Repo["AppointmentRepository"]
        UoW["UnitOfWork<br/>SERIALIZABLE + retry"]
        Ctx["SchedulerDbContext"]
        Seed["DbInitializer"]
    end

    subgraph Domain["KeyloopScheduler.Domain — zero external dependencies"]
        Ent["Entities<br/>Appointment · ServiceBay<br/>Technician · ServiceType · Dealership"]
        VO["Value objects<br/>TimeWindow · Vin"]
        Rules["ITechnicianQualificationRule<br/>+ CertificationQualificationRule"]
        Abs["Abstractions<br/>IAppointmentBookingService · IResourceAvailabilityQuery<br/>IResourceCatalogQuery · IAppointmentRepository · IUnitOfWork"]
        Exc["Domain exceptions<br/>DomainValidationException · ScheduleConflictException<br/>EntityNotFoundException"]
    end

    PG[("PostgreSQL 16")]

    Client --> Corr --> Ctrl
    Ctrl --> Filter
    Ctrl -->|depends on abstractions| Abs
    Ctrl -.-> Swagger

    Infra -.->|implements| Abs
    Booking --> Repo
    Booking --> UoW
    Booking --> Avail
    Booking --> Catalog
    Repo --> Ctx
    Avail --> Ctx
    Catalog --> Ctx
    UoW --> Ctx
    Ctx --> PG
    Seed --> Ctx

    Ent --- VO
    Ent --- Exc
    Rules --> Ent

    DI -.->|composition root| Infra
```

### Layer responsibilities

| Layer | Owns | Depends on |
|---|---|---|
| **Domain** | Entities, value objects, domain rules, exceptions, and the **abstractions** the outside depends on | *Nothing* — not EF Core, not ASP.NET, not Npgsql |
| **Infrastructure** | EF Core mapping, migrations, repositories, the serializable unit of work, the booking engine, seeding, DI composition | Domain |
| **Api** | HTTP negotiation, DTOs, error translation, Swagger, correlation IDs | Domain, Infrastructure |
| **Tests** | Tier 1 domain units, Tier 2 HTTP contracts, Tier 3 race simulation | All |

The dependency arrow never points inwards. `AppointmentBookingService` does not know
EF Core exists — it depends on `IAppointmentRepository`, `IUnitOfWork`,
`IResourceCatalogQuery`, `IResourceAvailabilityQuery` and `ITechnicianQualificationRule`,
all declared in the Domain.

---

## 2. End-to-End Data Flow

### 2.1 Booking an appointment

```mermaid
sequenceDiagram
    autonumber
    participant C as Client
    participant Ctrl as AppointmentsController
    participant Svc as AppointmentBookingService
    participant Cat as IResourceCatalogQuery
    participant Av as IResourceAvailabilityQuery
    participant UoW as UnitOfWork
    participant DB as PostgreSQL

    C->>Ctrl: POST /api/appointments
    Ctrl->>Ctrl: new Vin(request.Vin)
    Ctrl->>Svc: BookAsync(BookingRequest)

    Svc->>Cat: DealershipExistsAsync / GetServiceTypeAsync
    Cat->>DB: SELECT (no tracking)
    Svc->>Svc: TimeWindow(start, start + duration)

    Note over Svc,Av: Optimistic selection, outside any transaction
    Svc->>Cat: GetActiveServiceBaysAsync / GetActiveTechniciansAsync
    Svc->>Svc: filter technicians via ITechnicianQualificationRule
    Svc->>Av: IsServiceBayAvailableAsync / IsTechnicianAvailableAsync
    Av->>DB: overlap SELECT (compound index scan)

    Svc->>UoW: ExecuteInTransactionAsync(...)
    UoW->>DB: BEGIN ISOLATION LEVEL SERIALIZABLE
    UoW->>DB: overlap re-check inside the transaction

    alt No conflicting writer
        UoW->>DB: INSERT appointment
        UoW->>DB: COMMIT
        UoW-->>Svc: BookingResult
        Svc-->>Ctrl: BookingResult
        Ctrl-->>C: 201 Created + Location + body
    else Conflict, or SQLSTATE 40001 / 40P01
        UoW->>DB: ROLLBACK
        UoW-->>Svc: throw ScheduleConflictException
        Svc-->>Ctrl: propagates
        Ctrl-->>C: 409 Conflict (application/problem+json)
    end
```

The **double check is the crux**: resource selection happens optimistically before the
transaction, and the conflict predicate is evaluated *again* inside a `SERIALIZABLE`
transaction immediately before the insert. Two clients may both pass the optimistic
check; only one can pass the in-transaction check or commit without a serialization
failure.

### 2.2 Availability search

```mermaid
sequenceDiagram
    autonumber
    participant C as Client
    participant Ctrl as AvailabilityController
    participant Q as ResourceAvailabilityQuery
    participant DB as PostgreSQL

    C->>Ctrl: GET /api/availability?dealershipId&serviceTypeId&date
    Ctrl->>Q: FindAvailableSlotsAsync
    Q->>DB: SELECT service_types (duration + required certification)
    Q->>DB: SELECT active service_bays
    Q->>DB: SELECT active technicians
    Q->>Q: apply ITechnicianQualificationRule
    Q->>DB: SELECT that day's scheduled appointments<br/>for those bays/technicians (single query)
    Q->>Q: walk the 15-minute grid 08:00→18:00
    Q->>Q: emit (bay, technician) pairs free for the whole duration
    Q-->>Ctrl: IReadOnlyList<AvailabilitySlot>
    Ctrl-->>C: 200 OK
```

Availability is intentionally read-only and decoupled from the write path, so it can
later be served by a read replica or a dedicated CQRS read model without touching the
booking engine.

### 2.3 Cancellation

`DELETE /api/appointments/{id}` loads the aggregate, calls `appointment.Cancel(nowUtc)`
(which transitions `Scheduled → Cancelled` and stamps `CancelledAtUtc`), and saves
inside the same transactional unit of work. Because every overlap predicate filters on
`Status = Scheduled`, cancellation releases both resources atomically.

---

## 3. Concurrency Strategy

```mermaid
flowchart LR
    A["Request A<br/>BEGIN SERIALIZABLE"] --> B["conflict check<br/>→ none"]
    C["Request B<br/>BEGIN SERIALIZABLE"] --> D["conflict check<br/>→ none"]
    B --> E["INSERT A"]
    D --> F["INSERT B"]
    E --> G["COMMIT A"]
    F --> H["COMMIT B"]
    G --> I{"PostgreSQL<br/>serialization graph"}
    H --> I
    I -->|winner| J["201 Created"]
    I -->|loser: 40001| K["ROLLBACK → 409 Conflict"]
```

Three independent safety nets, in order:

1. **SQL-side overlap predicate** — half-open interval test
   (`existing.start < candidate.end AND candidate.start < existing.end`) evaluated by
   PostgreSQL, never by pulling rows into memory.
2. **`SERIALIZABLE` isolation** — PostgreSQL's SSI detects the read/write dependency
   between two concurrent booking transactions and aborts one with SQLSTATE `40001`.
3. **Defence in depth** — `ScheduleConflictException` for a detected conflict and
   `DbUpdateConcurrencyException` are both mapped to `409`, and the row-level predicate
   is re-evaluated inside the transaction.

Contention classification walks the **entire inner-exception chain**, because EF Core
wraps the `PostgresException` in a `DbUpdateException`, which the retry strategy may then
wrap again in a transient-failure envelope. See `docs/ai-refinement-log.md` — this was
a real defect found by the Tier 3 suite and is the single most important correctness fix
in the project.

---

## 4. Persistence

### Schema

| Table | Notes |
|---|---|
| `dealerships` | Ownership root |
| `service_bays` | `is_active` flag participates in availability |
| `technicians` | `certifications integer[]` — native PostgreSQL array so "holds certification X" is answered in SQL |
| `service_types` | `duration_minutes`, `required_certification` |
| `appointments` | `start_time_utc` / `end_time_utc` as `timestamptz`, `vin` via value conversion, `status` as text |

### Indexes (mandated, present in the initial migration)

```
ix_appointments_bay_window                 (service_bay_id, start_time_utc, end_time_utc)
ix_appointments_technician_window          (technician_id, start_time_utc, end_time_utc)
ix_appointments_dealership_status_start    (dealership_id, status, start_time_utc)
ix_service_bays_dealership_active          (dealership_id, is_active)
ix_technicians_dealership_active           (dealership_id, is_active)
```

The two window indexes match the overlap predicate's shape so contention checks are
index scans rather than full table scans.

### Interval modelling

The domain owns intervals through the immutable `TimeWindow` record struct, which
validates UTC kind, ordering, and all interval math (`OverlapsWith`, `IsAdjacentTo`,
`Contains`, `Encloses`). It is persisted as two `timestamptz` columns rather than an
opaque range type specifically so the mandated compound indexes stay usable and the
overlap predicate translates cleanly to SQL.

---

## 5. Domain Invariants

1. **Dual-resource allocation** — an appointment is valid only if both a compatible bay
   and a certified technician are unbooked for the entire duration.
2. **Certification gate** — `Technician.Certifications` must contain
   `ServiceType.RequiredCertification`; enforced through the pluggable
   `ITechnicianQualificationRule` (open/closed).
3. **Business hours & quantization** — 08:00–18:00 UTC, single day, starts on a
   15-minute boundary.
4. **Strict UTC** — every timestamp is `DateTimeKind.Utc`; `DateTime.Now` is banned and
   the clock is abstracted behind `TimeProvider`.
5. **Deterministic VIN** — 17 characters, uppercase alphanumerics excluding I, O, Q.
6. **Only `Scheduled` occupies resources** — `Cancelled` and `Completed` release them.
7. **No bookings in the past** — enforced in `Appointment.Schedule` against the injected
   clock.

---

## 6. Chosen Technologies & Trade-offs

| Decision | Chosen | Rejected alternative | Rationale |
|---|---|---|---|
| Runtime | **.NET 8 LTS** | .NET 9 / 10 | Assessment mandates .NET 8 LTS strictly |
| Isolation | **`Serializable` + in-transaction re-check** | `SELECT ... FOR UPDATE` row locks | SSI needs no lock ordering discipline and scales better; the retry-on-`40001` path is explicit and testable |
| Interval persistence | **Two `timestamptz` columns** | `tstzrange` + GiST exclusion constraint | Keeps the mandated compound indexes usable and the predicate portable; the `tstzrange` approach would be the natural next hardening step |
| Transaction boundary | **`IUnitOfWork.ExecuteInTransactionAsync`** | `DbContext.Database.BeginTransaction` in the service | Keeps EF Core out of the application service (DIP) |
| Clock | **`TimeProvider`** | `DateTime.UtcNow` | Deterministic tests |
| Qualification | **`ITechnicianQualificationRule`** | Inline certification comparison | New certification policies without touching the engine (OCP) |
| Read/write surfaces | **Separate `IResourceAvailabilityQuery` / `IAppointmentBookingService`** | Single god-service | ISP + a clean path to CQRS / read replicas |
| Error contract | **RFC 7807 `problem+json` everywhere** | Ad-hoc error JSON | Uniform, machine-readable errors including model-binding failures |
| Test DB | **Testcontainers `postgres:16`** | In-memory provider / SQLite | Behaviour under real PostgreSQL isolation and SQL semantics is the thing being tested |
| Retry | **`EnableRetryOnFailure` + explicit contention classification** | No retries | Satisfies the reliability requirement while still converting serialization failures into deterministic `409`s |

### Documented trade-offs and known limitations

- **Bay capability is not modelled.** AGENTS.md only mandates a *technician* gate, so
  all bays are treated as compatible with all services ("compatible bay" = free bay).
  Adding a bay-specialisation rule is a natural extension via a second pluggable rule.
- **Serializable retries add latency to the losing request.** The loser may be retried
  by EF before the failure is classified, so a contended request is slower than an
  uncontended one. Conflicts are expected outcomes for clients, and correctness is
  unaffected.
- **Availability returns a cartesian product.** Each free `(bay, technician)` pair per
  slot is returned, which is correct but can be verbose. A paginated or
  "best pairing only" projection would reduce payload size.
- **Migrations run at startup in Development only.** Production would apply migrations
  as a separate deployment step.
- **No authentication or authorization.** Out of scope for Scenario A.
- **UTC-only business hours.** Dealership-local opening hours and DST handling are not
  modelled.
- **No appointment rescheduling endpoint.** `Appointment.Reschedule` exists and is unit
  tested, but is not yet exposed over HTTP.

---

## 7. Observability & Monitoring Plan

### Implemented

- **Structured JSON logs.** `AddJsonConsole` with `IncludeScopes` and UTC timestamps —
  verified that every emitted line parses as JSON.
- **Correlation IDs.** `CorrelationIdMiddleware` honours an incoming `X-Correlation-Id`,
  generates one when absent, pushes it into the log scope, sets `HttpContext.TraceIdentifier`,
  and echoes it on the response. It is also surfaced as `traceId` inside every
  `problem+json` body, so a client-reported error maps directly to server logs.
- **Booking outcome telemetry.** Every booking emits
  `Booking outcome {Outcome} DealershipId ServiceTypeId ServiceBayId TechnicianId AppointmentId DurationMs`,
  with `Outcome = Booked` (Information) or `Outcome = Conflict` (Warning).
- **Conflict logging.** The unit of work logs a warning with the PostgreSQL `SqlState`
  whenever contention forces a rollback.
- **ASP.NET Core request diagnostics.** Method, path, status code and elapsed
  milliseconds per request, plus built-in `TraceId`/`SpanId` scopes.
- **Health probe.** `GET /health`.

### Monitoring plan

| Signal | Source | Alert |
|---|---|---|
| Conflict rate | count of `Outcome = Conflict` vs `Outcome = Booked` | Spike indicates hot-spot slots or retry storms |
| p95 booking latency | `DurationMs` on booking outcomes + request diagnostics | Regression in the transactional path |
| Serialization failures | `SqlState = 40001` warnings | Sustained rate implies contention beyond design assumptions |
| Deadlocks | `SqlState = 40P01` warnings | Should be near zero; SSI makes them rare |
| 5xx rate | `GlobalExceptionFilter` errors | Any unexpected 500 is a defect |
| Index health | `pg_stat_user_indexes` on the window indexes | Unused/duplicated indexes |

For production, the JSON formatter maps directly onto a log pipeline (e.g. Serilog +
OpenTelemetry export to an APM) with no application changes, and the existing
`TraceId`/`CorrelationId` scope keys provide the join between logs and traces.

---

## 8. Test Strategy

| Tier | Project location | Count | Infrastructure | Category filter |
|---|---|---|---|---|
| **1 — Domain units** | `Domain/` | 44 methods → 68 cases | None (in-memory) | `Category=Domain` |
| **2 — Integration & contract** | `Integration/` | 24 cases | `WebApplicationFactory<Program>` + Testcontainers `postgres:16` | `Category=Integration` |
| **3 — Concurrency** | `Concurrency/` | 2 cases | Dedicated Testcontainers instance, trimmed to 1 bay + 1 technician | `Category=Concurrency` |

Tier 3 asserts that N simultaneous requests for the identical slot yield **exactly one
`201`**, the remainder **`409`**, and exactly one row — with a 10-round × 5-contender
regression guard and a zero-duplicate invariant query.

---

## 9. Generative AI Collaboration Summary

This solution was built with an AI coding agent (Sisyphus / OhMyOpenCode) under a
spec-first discipline driven by `AGENTS.md`.

**Division of labour.** The agent scaffolded the solution, installed and pinned the
toolchain (.NET 8 SDK was absent — only .NET 10 was present), authored the domain
contract, implemented Infrastructure and Api, and wrote all three test tiers. The human
retained every consequential decision: scope (vertical slice vs. full test suite),
test-database strategy, and acceptance of each design trade-off.

**Where the AI needed correction.** Three genuine defects were introduced and caught by
the process, all recorded in `docs/ai-refinement-log.md`:

1. **The concurrency misclassification (500 instead of 409).** This is the most
   instructive failure. The AI's first implementation inspected only one level of the
   inner-exception chain; EF Core's retry strategy wraps the `PostgresException` two
   levels deep. The Tier 3 suite caught it — but only *flakily*, roughly once in twenty
   runs, and the very first full run was green. Had that green run been trusted, the
   defect would have shipped. Only escalating to a 20-round × 6-contender stress harness
   made it reproducible, after which the fix (walk the whole chain) was verified across
   600 concurrent attempts.
2. **A missing spec requirement.** Tier 2 in `AGENTS.md` requires `400` for past dates;
   the domain did not enforce it. Writing the test exposed the gap.
3. **A silent no-op fix.** The first attempt at a uniform RFC 7807 error contract used
   `ObjectResult.ContentTypes`, which appeared to work in a unit assertion but was
   ignored at runtime for `ValidationProblemDetails`. Only live `curl` verification
   caught it; the working fix used `JsonResult.ContentType`.

**What worked well.** The frozen domain contract; writing tests as executable
specifications rather than afterthoughts; verifying claims against a real PostgreSQL
container and live HTTP calls instead of trusting green checkmarks; and treating
flakiness as a defect signal rather than noise.

**Process lesson.** The strongest signal in this build was not "tests pass" but
"tests pass *repeatedly under load*". A single green run of a concurrency suite is
close to worthless evidence.
