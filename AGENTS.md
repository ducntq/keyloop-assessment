# AGENTS.md — AI Agent System & Execution Contract

## 1. Project Mission & Context
- **Project**: Keyloop Technical Assessment — Scenario A: The Unified Service Scheduler (Ownership Domain).
- **Core Mission**: Build a production-grade, resource-constrained automotive appointment scheduling engine.
- **Primary Technical Challenge**: Coordinate dual physical and human resource constraints (Service Bay + Qualified Technician) across continuous time windows with zero tolerance for double-booking race conditions.

## 2. Scope Boundaries & Layer Selection
- **Selected Layer**: Backend RESTful API with PostgreSQL persistence.
- **Strict Prohibition**: **DO NOT generate any frontend code** (HTML, CSS, JavaScript, React, Blazor).
- **Client Mocking Strategy**: The client-side layer is mocked exclusively via:
  1. Interactive OpenAPI 3.0 specification via Swagger UI (`/swagger`).
  2. Standalone runnable HTTP scripts (`requests.http` / cURL scripts).
  3. Automated integration test harnesses in `KeyloopScheduler.Tests`.

## 3. Technology Stack & Target Runtime
- **Target Framework**: C# / .NET 8 (LTS strictly; do not use .NET 9).
- **Persistence**: Entity Framework Core 8, Npgsql (PostgreSQL 16).
- **API Documentation**: Swashbuckle (`Swashbuckle.AspNetCore` for .NET 8).
- **Testing**: xUnit, `Microsoft.AspNetCore.Mvc.Testing`, `Testcontainers.PostgreSql`.
- **Solution Layout**:
  - `KeyloopScheduler.Domain`: Pure domain entities, value objects, domain exceptions, domain rules (zero external dependencies).
  - `KeyloopScheduler.Infrastructure`: `SchedulerDbContext`, EF Core configurations, migrations, persistence services, seeders.
  - `KeyloopScheduler.Api`: REST Controllers, DTOs, Exception Filters, `Program.cs`.
  - `KeyloopScheduler.Tests`: Unit, integration, and concurrent race-condition test suites.

## 4. Architectural Paradigms: DDD & SOLID
- **Domain-Driven Design (DDD)**:
  - **Zero Dependency Domain**: `KeyloopScheduler.Domain` must not reference EF Core, ASP.NET, or Npgsql.
  - **No Anemic Domain Models**: Core entities (`Appointment`, `ServiceBay`, `Technician`) must encapsulate behavior with private setters and factory methods.
  - **Explicit State Transitions**: Mutate state through explicit domain methods (e.g., `appointment.Cancel()`, `appointment.Reschedule(...)`).
  - **Value Objects**:
    - Encapsulate appointment intervals inside an immutable `TimeWindow` (C# `record struct`).
    - Validate UTC adherence and enforce interval math (`OverlapsWith(TimeWindow other)`) within `TimeWindow`.
    - Encapsulate vehicle identification inside a `Vin` value object validating standard 17-character format.
- **SOLID Principles**:
  - **Single Responsibility (SRP)**: Controllers negotiate HTTP; Domain models guard business invariants; Application handlers orchestrate workflows and transactions; Infrastructure handles SQL.
  - **Open/Closed (OCP)**: Technician qualification rules must implement an `ITechnicianQualificationRule` interface to allow new certification policies without altering the booking engine.
  - **Liskov Substitution (LSP)**: Abstract system time via .NET 8 `TimeProvider` to allow deterministic time travel in tests.
  - **Interface Segregation (ISP)**: Segregate query and command interfaces (`IResourceAvailabilityQuery` for reads, `IAppointmentBookingService` for writes).
  - **Dependency Inversion (DIP)**: High-level application flows depend strictly on domain abstractions (`IAppointmentRepository`, `IUnitOfWork`), not concrete EF contexts.

## 5. Test-Driven Development (TDD) Protocol
You must operate using a **Spec-First / Test-Driven approach**. Draft or verify tests in `KeyloopScheduler.Tests` before finalizing service implementations.

Tests must be organized into 3 explicit tiers:
1. **Tier 1: Domain Unit Tests** (Fast, in-memory, zero DB dependencies)
   - Duration calculation: `EndTimeUtc = StartTimeUtc + ServiceType.DurationMinutes`.
   - `TimeWindow` overlap matrix: Adjacent slots (touching boundaries allowed), partial overlaps, inner overlaps, and exact overlaps.
   - Skill validation: Rejection when technician lacks `ServiceType.RequiredCertification`.
   - State handling: Verify cancelled appointments release resource allocation.
2. **Tier 2: Integration & Contract Tests** (`WebApplicationFactory<Program>` + Postgres)
   - Happy path: `POST /api/appointments` returns `201 Created` with full relational IDs.
   - Availability search: `GET /api/availability` returns only slots where BOTH bay and qualified technician are free.
   - Resource contention: Return `409 Conflict` when bay is free but no certified technician is on shift.
   - Input validation: Return `400 Bad Request` on non-UTC timestamps, past dates, or malformed VINs.
3. **Tier 3: Concurrency Race-Condition Test** (Multi-client simulation)
   - Seed database with exactly 1 bay and 1 qualified technician.
   - Dispatch two simultaneous `POST /api/appointments` requests for the exact same slot via `Task.WhenAll`.
   - Assert: Exactly one request returns `201 Created`, the competing request returns `409 Conflict`, and exactly one appointment record exists in the database.

## 6. Domain Invariants & Business Rules
1. **Dual-Resource Allocation**: An appointment is valid if and only if both a compatible `ServiceBay` AND a qualified `Technician` are unbooked for the entire contiguous service duration.
2. **Skill Certification Gate**: A technician can be assigned if and only if `Technician.Certifications` contains `ServiceType.RequiredCertification`.
3. **Business Hours & Quantization**:
   - Operating hours are strictly 08:00 to 18:00 UTC. Bookings extending outside this window are rejected (`400 Bad Request`).
   - Appointments must be quantized to 15-minute intervals (minutes must be 00, 15, 30, or 45).
4. **Deterministic UTC Timestamps**:
   - Every timestamp stored and returned must be strict UTC (`DateTimeKind.Utc`).
   - Never use `DateTime.Now` or unzoned timestamps.
5. **Active Status Verification**: Only appointments with status `Scheduled` occupy resources; `Cancelled` appointments release availability.

## 7. Concurrency, Transactions & Safety
- **No In-Memory Filtering**: Resource overlap detection must execute inside PostgreSQL via transactional queries—never via client-side LINQ queries pulling full tables into memory.
- **Transactional Isolation**: Appointment reservations must execute inside an EF Core transaction with `IsolationLevel.Serializable` or using PostgreSQL row-level locks (`SELECT ... FOR UPDATE`).
- **Postgres Serialization Error Handling**: If PostgreSQL raises SQLSTATE `40001` (`serialization_failure`) or EF Core raises `DbUpdateConcurrencyException`, treat this as booking contention.
- **HTTP 409 Conflict Handling**: If either resource is taken or a concurrency collision occurs:
  - Abort and roll back the transaction immediately.
  - Throw a domain-specific `ScheduleConflictException`.
  - The Global Exception Filter must format this as an RFC 7807 response with HTTP status **409 Conflict**.

## 8. Data Seeding & Development Experience
- Provide a `DbInitializer` invoked in `Program.cs` during Development mode:
  - 1 Dealership (`Main Dealership`).
  - 3 Service Bays: Bay 1 (General Lift), Bay 2 (Alignment Rack), Bay 3 (EV Specialized Bay).
  - 4 Technicians: Tech A (`GENERAL`), Tech B (`GENERAL`, `BRAKES`), Tech C (`EV_CERTIFIED`, `GENERAL`), Tech D (`DIESEL`, `BRAKES`).
  - 3 Service Types:
    - Oil & Inspection (30 mins, requires `GENERAL`).
    - Brake Pad Replacement (60 mins, requires `BRAKES`).
    - EV Battery Diagnostics (90 mins, requires `EV_CERTIFIED`).

## 9. "Build For The Future" Standards
- **Scalability**: Decouple availability checks (`GET /api/availability`) from write mutations (`POST /api/appointments`) to allow future CQRS or read replicas.
- **Performance**: Include compound database indexes on `(ServiceBayId, StartTimeUtc, EndTimeUtc)` and `(TechnicianId, StartTimeUtc, EndTimeUtc)` to eliminate full table scans.
- **Reliability**: Implement connection retries and automatic rollback on transaction deadlocks.
- **Observability**: Emit structured JSON logs with correlation IDs capturing `DealershipId`, `ServiceTypeId`, `DurationMs`, and conflict outcomes.

## 10. Developer Commands & Verification Loop
Before declaring any task complete, execute this sequence in the terminal:
1. `dotnet build --nologo` (must pass with 0 errors and 0 warnings).
2. `dotnet test --filter Category!=Concurrency --nologo` (Tier 1 & Tier 2 tests must pass).
3. `dotnet test --filter Category=Concurrency --nologo` (Tier 3 concurrency test must pass).

## 11. AI Agent Reflection & Documentation Protocol
- **AI Flaw Logging**: When encountering a bug, failed test, or flawed initial generation (e.g., race condition in tests, timezone mismatch, serialization error):
  1. Record the issue, prompt, root cause, and fix in `docs/ai-refinement-log.md`.
  2. Synthesize this data into the final `README.md` under the section `## AI Collaboration Narrative`.
- **System Design Document**: Maintain `docs/system-design.md` containing:
  - Component Architecture Diagram (Mermaid format).
  - End-to-end Data Flow Explanation.
  - Chosen Technologies & Architectural Trade-offs.
  - Observability & Monitoring Plan.
  - Generative AI Collaboration Summary.
