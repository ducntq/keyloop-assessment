# AI Refinement Log

Bugs, failed tests and flawed first-pass generations encountered while building the
Unified Service Scheduler, with their root causes and fixes. Synthesised into the
`## AI Collaboration Narrative` section of the README.

---

## 1. Concurrency loser returned HTTP 500 instead of 409

**Where**: `src/KeyloopScheduler.Infrastructure/Persistence/UnitOfWork.cs`
**Caught by**: Tier 3 concurrency suite (flaky — roughly 1 failure in 20 runs)

### Symptom

Two simultaneous `POST /api/appointments` requests for the same slot should produce
exactly one `201 Created` and one `409 Conflict`. Under load the loser intermittently
returned `500 Internal Server Error` instead:

```
round 3: [409, 500, 201, 500, 409, 409]
```

The unit-of-work logged it through the *generic* failure branch, not the contention
branch:

```
fail: KeyloopScheduler.Infrastructure.Persistence.UnitOfWork[0]
      Booking transaction failed and was rolled back.
      System.InvalidOperationException: An exception has been raised that is likely
        due to a transient failure.
       ---> Microsoft.EntityFrameworkCore.DbUpdateException: An error occurred while
            saving the entity changes.
       ---> Npgsql.PostgresException (0x80004005): 40001: could not serialize access
            due to read/write dependencies among transactions
```

### Root cause

PostgreSQL correctly raised SQLSTATE `40001` under `SERIALIZABLE` isolation — the
expected outcome. The defect was in **exception classification**. The original guard
inspected only one level of the inner-exception chain:

```csharp
ex is DbUpdateConcurrencyException ||
IsSerializationOrDeadlock(ex) ||
(ex.InnerException is not null && IsSerializationOrDeadlock(ex.InnerException));
```

But because `EnableRetryOnFailure` is configured (AGENTS.md §9 requires connection
retries), EF Core's execution strategy retried the doomed insert and, on exhausting
its retries, wrapped the failure in an additional envelope. The real chain is **three**
levels deep, so the check missed it:

```
InvalidOperationException  (retry strategy envelope)
  └─ DbUpdateException      (EF Core)
      └─ PostgresException  SQLSTATE 40001   ← the actual match, two levels down
```

The failure was therefore misclassified as an unknown fault and surfaced as `500`.

### Fix

Walk the entire inner-exception chain, and treat either `DbUpdateConcurrencyException`
or a `40001`/`40P01` PostgreSQL error at any depth as booking contention:

```csharp
for (var current = exception; current is not null; current = current.InnerException)
{
    if (current is DbUpdateConcurrencyException) return true;
    if (current is PostgresException pg &&
        (pg.SqlState == PostgresErrorCodes.SerializationFailure ||
         pg.SqlState == PostgresErrorCodes.DeadlockDetected)) return true;
}
```

### Verification

A throwaway harness drove 20–40 rounds × 5–6 simultaneous clients against a
single-bay/single-technician catalogue. Before the fix it reproduced the `500` within
a few rounds; after the fix, 5 consecutive runs (600 concurrent attempts) produced
exactly one `201` and the remainder `409`, with zero duplicate rows.

A permanent regression guard was promoted into the Tier 3 suite
(`Repeated_races_never_double_book_the_single_resource_pair`), which now runs 10 rounds
× 5 contenders and asserts every response is either `201` or `409`. Tier 3 passed
10/10 consecutive runs after the fix.

### Lesson

When a persistence layer both wraps exceptions *and* applies a retry strategy, the
domain-significant error code can sit arbitrarily deep in the chain. Classify on the
whole chain, never on a fixed depth.

---

## 2. Automatic model-validation 400 returned `application/json`, not RFC 7807

**Where**: `src/KeyloopScheduler.Api/Program.cs`
**Caught by**: Tier 2 validation contract test

### Symptom

Domain-thrown `400`s correctly returned `application/problem+json` via the global
exception filter, but a DataAnnotations failure (e.g. a 9-character VIN) returned
`application/json` — an inconsistent error contract.

### Root cause

`[ApiController]`'s automatic model-state filter uses its own response factory and does
not route through the exception filter. Its default result is a `BadRequestObjectResult`,
which content-negotiates to `application/json`.

First fix attempt — returning an `ObjectResult` with
`ContentTypes = { "application/problem+json" }` — did **not** work: the custom `type`
and `traceId` in the body proved the factory ran, yet the content type was still
`application/json`.

### Fix

Return a `JsonResult` with an explicit content type, which the executor honours:

```csharp
return new JsonResult(problem)
{
    StatusCode = StatusCodes.Status400BadRequest,
    ContentType = "application/problem+json"
};
```

### Verification

Confirmed by live `curl` against the running API (`Content-Type: application/problem+json`),
and locked in by the Tier 2 contract tests.

---

## 3. EF Core 9 API used against an EF Core 8 target

**Where**: `tests/KeyloopScheduler.Tests/Infrastructure/SchedulerApiFactory.cs`

### Symptom

`error CS0246: The type or namespace name 'IDbContextOptionsConfiguration<>' could not be found`

### Root cause

The test factory removed the application's `DbContext` registration using
`RemoveAll<IDbContextOptionsConfiguration<SchedulerDbContext>>()`. That type is public in
**EF Core 9+**, but does not exist in **EF Core 8.0.11**, which this project targets
strictly. The compile error surfaced the version mismatch.

### Fix

EF Core 8 `AddDbContext` registers only `DbContextOptions<TContext>`,
`DbContextOptions` and the context itself, so removing those three is sufficient. The
reference to the non-existent type was dropped.

### Verification

Test project compiles with 0 warnings / 0 errors; the swapped registration is proven
correct because every Tier 2/3 test migrates, seeds and queries the container database
successfully.

### Lesson

Pin framework-targeted APIs against the exact installed SDK. Target framework and
runtime packages were correct; only the reference assembly version exposed the mismatch.
