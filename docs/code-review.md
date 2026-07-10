# Code review

Assessment of the repository as of 2026-07-10 (version 1.2.3, commit `a6de57c`).
The review covers source code, tests, CI/CD workflows, container packaging, and operational tooling.

## Overall assessment

The codebase is small, clean, and focused.
The three-project layering (`WebApi` → `Infrastructure.MongoDb` → `Domain`) is respected with one-way dependencies.
The Terraform HTTP backend protocol is implemented faithfully: raw `text/plain` state responses, `423 Locked`, `409 Conflict` with the current lock as body, and upsert on POST.
Multi-tenancy is enforced consistently through the tenant claim and the `TenantAuthorizationFilter`, and every MongoDB query filters on tenant.
CI is mature for a project of this size: Sonar, FOSSA, container CVE scanning with a zero-CVE budget, markdown/YAML linting, and scenario tests that run the real `terraform` CLI against a Kestrel-hosted instance.

The findings below are ordered by severity.
None of them blocks normal usage today, but several affect reliability under failure or concurrency.

## High severity

### H1. Health check does not verify MongoDB connectivity

`Program.cs` registers `AddHealthChecks()` with no checks, so `/health` returns `Healthy` even when MongoDB is unreachable.
In Kubernetes this makes readiness/liveness probes meaningless: the pod stays in rotation while every state operation fails with 500.
Fix: add a MongoDB ping check (e.g. `AspNetCore.HealthChecks.MongoDb` or a small custom `IHealthCheck` running `db.runCommand({ ping: 1 })`).

**Fixed on 2026-07-10**: `MongoDbHealthCheck` pings the database, with a test asserting `/health` returns 503 when MongoDB is unreachable.

### H2. `Application__*` environment variables do not match the `Features:*` configuration keys

`ApplicationConfiguration` reads `Features:IsScalarEnabled` and `Features:IsHttpsRedirectionEnabled`.
However `compose.yaml`, the CI workflow (`extra-vars`), and `IntegrationTestBase` all set `Application__*` variables.
Those bind to an `Application:` configuration section that nothing reads.
The mismatch is masked because `appsettings.Development.json` already enables Scalar and disables HTTPS redirection, so the overrides silently do nothing.
Verify whether `Withywoods.Configuration.TryGetSection` applies a fallback convention; if not, rename the environment variables to `Features__*` (or make the code read both) and add a test that proves an override takes effect.

**Fixed on 2026-07-10**: the environment variables were renamed to `Features__*`, and a test proves a `Features:IsScalarEnabled=false` override disables Scalar.

### H3. Lock acquisition is not atomic (check-then-insert race)

`StateController.Lock` does `FindOneAsync` then `CreateAsync`.
Two concurrent `terraform plan/apply` runs can both pass the null check; the unique index on `tenant + name` then makes the second `InsertOneAsync` throw `MongoWriteException`.
That exception surfaces as an unhandled 500 instead of the protocol-mandated 409 with the winning lock as body.
Fix: attempt the insert directly and translate a duplicate-key error into a re-read plus `409 Conflict`.
The same check-then-act pattern exists between the lock check in `Create`/`Delete` and the subsequent state write, though the practical impact there is much lower because Terraform holds the lock across the whole operation.

**Fixed on 2026-07-10**: `StateLockRepository.CreateAsync` now inserts first and maps duplicate-key errors to a null result, which the controller turns into `409 Conflict` with the winning lock as body.

## Medium severity

### M1. State JSON round-trips through MongoDB Extended JSON

`StateRepository.FindOneAsync` returns `document["value"].ToJson()`, and `CreateAsync` feeds the same `ToJson()` output into the JSON diff.
`BsonDocument.ToJson()` emits MongoDB Extended JSON, not strict JSON, so type fidelity depends on how the driver parsed the original document (for example large integers, doubles that look like integers, or `$`-prefixed keys).
Terraform state is plain JSON so this works in practice, but the guarantee is implicit.
Consider serializing with explicit `JsonWriterSettings` (strict/relaxed mode), or storing the raw state string alongside the parsed document, plus a round-trip test with a state file containing edge-case values.

### M2. Malformed `Authorization` header returns 500

`BasicAuthenticationHandler` calls `Convert.FromBase64String` on the header value without a try/catch.
A client sending `Authorization: Basic not-base64!` gets a 500 `FormatException` instead of a 401.
Wrap the decode and return `AuthenticateResult.Fail` on malformed input.

**Fixed on 2026-07-10**: malformed Base64 now yields `AuthenticateResult.Fail`, and a test asserts the response is 401.

### M3. `createdAt` in `tf_state` is overwritten on every update

`StateRepository.CreateAsync` always writes `createdAt = DateTime.UtcNow`, so the field behaves as `updatedAt` and the original creation time is lost.
Keep the existing `createdAt` on update and add a separate `updatedAt` field.

### M4. Raw `text/plain` state POST likely fails with 500

`RawRequestBodyFormatter` binds a `text/plain` (or missing content-type) body as a `string`.
`StateController.Create` then runs `JsonSerializer.Serialize(input)`, which double-encodes the string (`"{\"a\":1}"` becomes a quoted JSON string), and `BsonDocument.Parse` throws.
Terraform sends `application/json` for state updates, so the scenario tests pass, but the formatter exists precisely for non-JSON content types.
Either detect the string case and use it directly, or add a test that posts state as `text/plain` and fix what it reveals.
Related detail: `CanRead` compares the content type by exact string, so `text/plain; charset=utf-8` is not matched.

### M5. BCrypt verification on every request, with no throttling

Each authenticated request costs a BCrypt verify at work factor 10 (~50–100 ms of CPU).
There is no rate limiting, no failed-login lockout, and no short-lived credential cache, so the endpoint is a cheap CPU-exhaustion target and adds constant latency to every Terraform operation.
Consider a bounded in-memory cache of successful credential hashes and ASP.NET rate limiting on authentication failures.

### M6. Cancellation tokens are not propagated

Controller actions and repository methods do not accept or forward `CancellationToken`, so aborted Terraform requests keep running their MongoDB queries.
Low effort to add (`HttpContext.RequestAborted` flows naturally through the driver's async APIs).

## Low severity

### L1. Route name constraint is unanchored and misleading

`{name:regex([[a-zA-Z]]+)}` only requires one letter somewhere in the value (the scenario tests themselves use names like `local-files-<guid>` with digits and hyphens).
Either anchor and widen it (for example `^[a-zA-Z][a-zA-Z0-9._-]*$`) or remove it and validate explicitly.

### L2. Authentication handler path workaround is redundant

The `/scalar/`/`/openapi/` prefix check in `BasicAuthenticationHandler` duplicates what `AllowAnonymous` on those endpoints already achieves, and `/scalar` without a trailing slash bypasses the check anyway.
It can most likely be deleted; confirm with the existing Scalar/OpenAPI integration tests.

### L3. History entries are hard to consume

`tf_state_history.upgrade` stores the JSON Patch as a string rather than a BSON document, and the field name does not convey "patch/diff".
Only the forward diff is kept, so reconstructing an older version requires replaying patches in the right direction with no API support.
This matters mostly for the planned history/revision features (see the [backlog](backlog.md)).

### L4. Version metadata is duplicated

`OpenApi.Version` (`v1.2` in `appsettings.json`) must be kept in sync with `VersionPrefix` (`1.2.3` in `Directory.Build.props`) by hand.
Consider injecting the assembly version into the OpenAPI document at startup.

### L5. Unbounded growth and stale data

`tf_state_history` has no TTL or retention policy, and locks never expire (a crashed run requires a manual `terraform force-unlock`).
Both deserve at least documentation, and optionally a TTL index or cleanup command in `tfbeadm`.

## Test coverage

The integration test suite covers the happy paths well: state create/read/delete, the full lock lifecycle including 423 and 409 responses, wrong-tenant 401, health, OpenAPI snapshot, and a real `terraform init/plan/apply` scenario.

Gaps worth closing (tracked in the [backlog](backlog.md)):

- No test for malformed or missing `Authorization` headers (would have caught M2).
- No concurrent lock-acquisition test (would have caught H3).
- No `text/plain` state POST test (would have caught M4).
- No assertion that a state update writes a `tf_state_history` entry.
- No test that configuration overrides via environment variables take effect (would have caught H2).
- No unit tests, which is acceptable while the domain has no logic, but the diff logic in `StateRepository` is worth testing in isolation.

## CI/CD and operations

- The CI pipeline installs MongoDB and Terraform inline via `custom-commands`; a `services:` container or the existing `compose.yaml` would be simpler and faster, though the current approach works.
- The Dockerfile is solid: SUSE BCI base images, non-root user, checksum-verified OpenTelemetry auto-instrumentation, no secrets baked in.
- `tfbeadm` is a pragmatic admin tool; note that it depends on `htpasswd` producing a `$2y$` BCrypt prefix (which `BCrypt.Net-Next` accepts) and that user creation interpolates values into a `mongosh` command, where quoting is fragile.
- The `demo` and `pkg` workflows were not reviewed in depth.

## Strengths worth preserving

- Strict protocol fidelity in `StateController`, documented in `CLAUDE.md` so future changes check the spec first.
- Scenario tests that exercise the real Terraform CLI end to end.
- Central package management with transitive pinning, and a zero-CVE budget on image scans.
- Global MongoDB conventions (camelCase, string enums) registered in one place.
