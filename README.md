# Keyloop Unified Service Scheduler

A production-grade, resource-constrained automotive appointment scheduling engine for
**Scenario A — The Unified Service Scheduler (Ownership Domain)**.

Every booking must simultaneously reserve **two** scarce resources — a physical
**service bay** and a **certification-qualified technician** — for a contiguous interval
of time, with zero tolerance for double-booking under concurrency.

Backend REST API only. No frontend: the client layer is mocked exclusively through
Swagger UI, `requests.http`, and the automated integration test harness.

---

## Highlights

- **DDD with a zero-dependency domain.** `KeyloopScheduler.Domain` references no EF Core,
  no ASP.NET, no Npgsql. Intervals are modelled by an immutable `TimeWindow` value object
  that owns all UTC validation and interval math.
- **Dual-resource booking, race-proof.** Optimistic resource selection plus an
  in-transaction re-check inside a `SERIALIZABLE` transaction. PostgreSQL serialization
  failures (`40001`) and deadlocks (`40P01`) are classified into deterministic
  `409 Conflict` responses.
- **SQL-side overlap detection.** No client-side filtering of appointments, ever.
  Compound indexes on `(ServiceBayId, StartTimeUtc, EndTimeUtc)` and
  `(TechnicianId, StartTimeUtc, EndTimeUtc)` keep contention checks as index scans.
- **Uniform RFC 7807 error contract.** Every error — domain, conflict, or model-binding —
  is `application/problem+json` with a `traceId`.
- **94 automated tests across three tiers**, including a real multi-client race
  simulation against containerized PostgreSQL 16.
- **Structured JSON logs** with correlation IDs and booking-outcome telemetry.

---

## Technology

| Concern | Choice |
|---|---|
| Runtime | .NET 8 (LTS) — pinned via `global.json` |
| Persistence | EF Core 8.0.11 + Npgsql + PostgreSQL 16 |
| API docs | Swashbuckle 6.6.2 (OpenAPI) |
| Tests | xUnit, FluentAssertions, `WebApplicationFactory`, Testcontainers.PostgreSql |

### Solution layout

```
KeyloopScheduler.sln
├── src/
│   ├── KeyloopScheduler.Domain           # Entities, value objects, rules, abstractions (no dependencies)
│   ├── KeyloopScheduler.Infrastructure   # EF Core, migrations, repositories, booking engine, seeding
│   └── KeyloopScheduler.Api              # Controllers, DTOs, exception filter, Swagger
├── tests/
│   └── KeyloopScheduler.Tests            # Tier 1 domain · Tier 2 integration · Tier 3 concurrency
└── docs/
    ├── system-design.md                  # Architecture, data flow, trade-offs, observability
    └── ai-refinement-log.md              # Defects found and fixed during development
```

See [`docs/system-design.md`](docs/system-design.md) for the full architecture, Mermaid
diagrams, and the trade-off rationale.

---

## Prerequisites

- **.NET 8 SDK** (8.0.x). `global.json` pins `8.0.425` with `latestPatch` roll-forward.
- **Docker** — required for the integration/concurrency tests (Testcontainers) and
  convenient for running PostgreSQL locally.
- **PostgreSQL 16** — either containerized or native.

---

## Getting started

### 1. Start PostgreSQL

```bash
docker run -d --name keyloop-pg \
  -e POSTGRES_PASSWORD=postgres \
  -e POSTGRES_USER=postgres \
  -e POSTGRES_DB=keyloop_scheduler \
  -p 5433:5432 postgres:16
```

### 2. Point the API at it

`appsettings.json` defaults to `localhost:5432`. Override via environment variable if
your database is elsewhere (e.g. the container above maps to `5433`):

```bash
export ConnectionStrings__DefaultConnection="Host=localhost;Port=5433;Database=keyloop_scheduler;Username=postgres;Password=postgres"
```

### 3. Run

```bash
dotnet run --project src/KeyloopScheduler.Api
```

In `Development` the app applies migrations and seeds automatically, then serves:

- Swagger UI → <http://localhost:5080/swagger>
- Health → <http://localhost:5080/health>

### 4. Try it

Open `src/KeyloopScheduler.Api/KeyloopScheduler.Api.http`, or use the seeded identifiers
below with Swagger UI.

---

## API

| Method | Route | Success | Notes |
|---|---|---|---|
| `POST` | `/api/appointments` | `201 Created` + `Location` | Reserves a bay **and** a qualified technician |
| `GET` | `/api/appointments/{id}` | `200 OK` | Retrieves an appointment |
| `DELETE` | `/api/appointments/{id}` | `204 No Content` | Cancels and releases both resources |
| `GET` | `/api/availability` | `200 OK` | Only slots where **both** resources are free |
| `GET` | `/health` | `200 OK` | Liveness probe |

Error responses are RFC 7807 `application/problem+json`:

| Status | Meaning |
|---|---|
| `400 Bad Request` | Non-UTC timestamp, past date, malformed VIN, out-of-hours or non-quantized start, uncertified technician |
| `404 Not Found` | Unknown dealership, service type, bay, technician, or appointment |
| `409 Conflict` | Bay and/or technician already booked, including concurrent contention |

### Example — book an appointment

```bash
curl -X POST http://localhost:5080/api/appointments \
  -H 'Content-Type: application/json' \
  -d '{
    "dealershipId": "a0000000-0000-0000-0000-000000000001",
    "serviceTypeId": "d0000000-0000-0000-0000-000000000001",
    "serviceBayId": null,
    "technicianId": null,
    "vin": "1HGBH41JXMN109186",
    "startTimeUtc": "2026-12-01T09:00:00Z"
  }'
```

Omitting `serviceBayId` / `technicianId` asks the engine to auto-assign the first
compatible free resources.

### Example — search availability

```bash
curl "http://localhost:5080/api/availability?dealershipId=a0000000-0000-0000-0000-000000000001&serviceTypeId=d0000000-0000-0000-0000-000000000001&date=2026-12-01"
```

---

## Seeded development data

| Entity | Values |
|---|---|
| Dealership | `Main Dealership` — `a0000000-0000-0000-0000-000000000001` |
| Service bays | `Bay 1 (General Lift)` `…b0000000-…-0001`, `Bay 2 (Alignment Rack)` `…0002`, `Bay 3 (EV Specialized Bay)` `…0003` |
| Technicians | `Tech A` [General] `…c0000000-…-0001`, `Tech B` [General, Brakes] `…0002`, `Tech C` [EV Certified, General] `…0003`, `Tech D` [Diesel, Brakes] `…0004` |
| Service types | `Oil & Inspection` 30 min → General `…d0000000-…-0001`, `Brake Pad Replacement` 60 min → Brakes `…0002`, `EV Battery Diagnostics` 90 min → EV Certified `…0003` |

---

## Domain rules

1. **Dual-resource allocation** — a booking is valid only if both a compatible bay and a
   certified technician are free for the entire duration.
2. **Certification gate** — the technician must hold `ServiceType.RequiredCertification`.
3. **Business hours** — 08:00–18:00 UTC, within a single day. Starts must be quantized to
   15-minute boundaries.
4. **Strict UTC** — all timestamps are `DateTimeKind.Utc`; the clock is abstracted behind
   `TimeProvider`.
5. **VIN format** — 17 characters, uppercase alphanumerics excluding `I`, `O`, `Q`.
6. **Cancellation releases resources** — only `Scheduled` appointments occupy resources.
7. **No bookings in the past.**

---

## Testing

Three tiers, separated by xUnit trait so each can be run independently.

```bash
# 1. Build must be clean: 0 errors, 0 warnings
dotnet build --nologo

# 2. Tier 1 (domain units) + Tier 2 (integration contracts)
dotnet test --filter Category!=Concurrency --nologo

# 3. Tier 3 (concurrency race simulation)
dotnet test --filter Category=Concurrency --nologo
```

| Tier | Category | Cases | What it proves |
|---|---|---|---|
| 1 | `Domain` | 68 | Interval-overlap matrix, business hours, quantization, VIN, entity lifecycle, qualification gate |
| 2 | `Integration` | 24 | Happy-path `201` + `Location` + relational IDs, availability correctness, dual-resource `409`, all `400`/`404` validation paths |
| 3 | `Concurrency` | 2 | N simultaneous requests for one slot → exactly one `201`, the rest `409`, exactly one row |

Tier 2 and Tier 3 spin up a throwaway **PostgreSQL 16** container via Testcontainers and
run the real API against it, including startup migration and seeding. Tier 3 reduces the
catalogue to exactly one bay and one qualified technician before firing simultaneous
requests, and includes a 10-round × 5-contender regression guard.

### Database migrations

`dotnet-ef` is pinned in a local tool manifest, so restore it first (needs NuGet access,
not Docker). Adding a migration does **not** require a running database — the
Infrastructure project provides an `IDesignTimeDbContextFactory`, so it can be its own
startup project:

```bash
dotnet tool restore
dotnet ef migrations add <Name> \
  --project src/KeyloopScheduler.Infrastructure \
  --startup-project src/KeyloopScheduler.Infrastructure \
  --output-dir Persistence/Migrations
```

---

## Design decisions at a glance

| Decision | Why |
|---|---|
| `SERIALIZABLE` + in-transaction re-check (not `SELECT … FOR UPDATE`) | No lock-ordering discipline required; explicit, testable contention path |
| Two `timestamptz` columns for the interval (not `tstzrange`) | Keeps the mandated compound indexes usable and the predicate portable |
| Domain-owned abstractions, implementations in Infrastructure | Identity/DIP — the booking engine never sees EF Core |
| Separate read (`IResourceAvailabilityQuery`) and write (`IAppointmentBookingService`) surfaces | Interface segregation and a clean path to CQRS or read replicas |
| `ITechnicianQualificationRule` | New certification policies without modifying the engine (open/closed) |
| `TimeProvider` | Deterministic time in tests |

Known limitations and further hardening options are documented in
[`docs/system-design.md` §6](docs/system-design.md).

---

## AI Collaboration Narrative

This solution was built by an AI coding agent (Sisyphus / OhMyOpenCode) working to the
`AGENTS.md` contract, with the human retaining all consequential decisions: scope,
test-database strategy, and acceptance of each trade-off.

**How the work was sequenced.** The agent began with environment reconnaissance, which
immediately surfaced two blockers: only the .NET 10 SDK was installed (the spec mandates
.NET 8 strictly), and Docker Desktop's WSL integration was disabled, making the required
Testcontainers strategy unusable. The .NET 8 SDK was installed and pinned via `global.json`;
the human enabled Docker. The agent then froze the domain contract first, because every
downstream layer and both parallel workstreams depend on it, and only afterwards built
Infrastructure, the API, and the three test tiers.

**A delegation failure worth recording.** The agent's first instinct was to delegate the
Infrastructure and API layers to parallel background sub-agents. Both launches returned
background task identifiers, but when the results were collected the tasks reported
"Task not found" and had produced no files at all — the sub-agent infrastructure never
registered them. The agent detected this by inspecting the filesystem rather than
trusting the launch acknowledgements, and completed both layers directly. The open
question is whether the tasks were rejected at launch or lost afterwards.

**Three defects were found and fixed, and the process that found them matters more than
the fixes.** All are recorded in [`docs/ai-refinement-log.md`](docs/ai-refinement-log.md):

1. **A concurrency bug that returned `500` instead of `409`.** The AI's contention
   classifier inspected only one level of the inner-exception chain, but EF Core's retry
   strategy nests the PostgreSQL `40001` two levels deep
   (`InvalidOperationException → DbUpdateException → PostgresException`). The Tier 3 suite
   caught it — *flakily*, roughly once in twenty runs — and **the very first full run was
   green**. Trusting that green run would have shipped the defect. The agent escalated to a
   throwaway 20-round × 6-contender stress harness, reproduced it, fixed it by walking the
   whole exception chain, and verified 600 concurrent attempts before promoting a permanent
   10-round × 5-contender regression guard. This is the single most important lesson in the
   project: for concurrency, "tests pass" is close to worthless evidence and "tests pass
   repeatedly under load" is the only real signal.
2. **A missing spec requirement.** `AGENTS.md` Tier 2 requires `400` for past dates; the
   domain did not enforce it. Writing the test exposed the gap, and the invariant was added
   to `Appointment.Schedule`.
3. **A silent no-op fix.** The first attempt at a uniform RFC 7807 error contract used
   `ObjectResult.ContentTypes`. It passed the assertion that was written for it, yet live
   `curl` showed the server still returning `application/json` — the property is ignored for
   `ValidationProblemDetails`. The working fix used `JsonResult.ContentType`. Green tests
   were not sufficient evidence; only exercising the running system was.

A fourth issue — using an EF Core 9-only type (`IDbContextOptionsConfiguration<>`) against
an EF Core 8 target — was caught immediately by the compiler.

**What the agent did well.** Freezing the domain contract before parallelising; writing
tests as executable specifications rather than afterthoughts; verifying claims against a
real PostgreSQL 16 container and live HTTP calls instead of trusting summaries; treating
flakiness as a defect signal; and noticing a missing non-functional requirement
(AGENTS.md §9 observability) while writing the documentation, rather than documenting a
capability that did not exist.

**Where human judgement was essential.** Choosing the .NET 8 toolchain, enabling Docker,
setting scope, and insisting that a green test run be interrogated rather than accepted.
