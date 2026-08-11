# Code review

Assessment of the repository as of 2026-08-11 (version 1.2.3, commit `d380eaa`).
It supersedes the review of 2026-07-10 (commit `a6de57c`): every earlier finding was re-checked against the current code, and the outcome is recorded in the [status of the previous review](#status-of-the-previous-review).
The review covers source code, tests, CI/CD workflows, container packaging, and operational tooling.

## Method

Findings are marked **Proven** when a command reproduced them against a running instance, and **Observed** when they come from reading the code alone.
The proven findings were reproduced against `dotnet run --project src/WebApi` backed by a freshly seeded MongoDB 8.2 database.
Reproduction commands are given inline so that each one can be turned into a regression test.

## Overall assessment

The codebase is small, clean, and focused.
The three-project layering (`WebApi` > `Infrastructure.MongoDb` > `Domain`) is respected with one-way dependencies.
The Terraform HTTP backend protocol is implemented faithfully at the level of status codes and lock semantics: raw `text/plain` state responses, `423 Locked`, `409 Conflict` with the current lock as body, and upsert on POST.
Multi-tenancy is enforced consistently through the tenant claim and the `TenantAuthorizationFilter`, and every MongoDB query filters on tenant.
CI is mature for a project of this size: Sonar, FOSSA, container CVE scanning with a zero-CVE budget, markdown and YAML linting, and scenario tests that run the real `terraform` CLI against a Kestrel-hosted instance.

The three high-severity findings of the previous review were fixed and the fixes hold up under inspection.
The theme of this round is different: the protocol layer is correct, but the **storage layer does not faithfully preserve the payload it is given**.
Terraform state is an opaque JSON document, and the application parses it into BSON, which is a lossy and size-limited representation.
H1 and H2 below are two faces of that single design decision, and both produce a failed `terraform apply` rather than a degraded experience.

## Verification of the NuGet upgrade

The package bump in `d380eaa` was verified, since it had not been tested when it was committed.

- `dotnet build` succeeds with 0 warnings and 0 errors.
- `dotnet test` passes 13 of 13 tests, including the `terraform init/plan/apply` scenario against a Kestrel-hosted instance.

No action is needed on the upgrade itself.
The first test run did fail 6 of 13 tests, for an environmental reason that is a finding in its own right: see [T1](#t1-the-test-suite-fails-confusingly-against-a-drifted-local-database).

## High severity

### H1. A state larger than 16 MB is rejected with an unhandled 500

**Proven.**
`StateRepository.CreateAsync` stores the state as a parsed `BsonDocument`, so a state document is bound by the MongoDB maximum BSON document size of 16 MB.

```bash
# builds an 18 MB state document
python3 -c "import json;open('big.json','w').write(json.dumps({'version':4,'serial':1,'resources':[{'name':'r%d'%i,'blob':'x'*900} for i in range(20000)]}))"
curl -u admin:admin123 -X POST http://localhost:5293/dummy/state/big \
  -H "Content-Type: application/json" --data-binary @big.json
```

The response is `500` with `System.FormatException: Size 18777785 is larger than MaxDocumentSize 16793600`.

Two details make this worse than a plain size limit.
The failure surfaces during `terraform apply`, after the lock has been taken and after the real infrastructure has been changed, which is the worst possible moment to lose a state write.
The history patch in `tf_state_history` is stored as a JSON *string* in a single document, so it is bound by the same 16 MB limit while being far less compact than the BSON it describes, and on an update it is written first.
The effective ceiling for updating a large state is therefore lower than 16 MB, and which of the two writes fails depends on how much the state changed.

Terraform states of this size are not exotic for a large workspace, and nothing in the documentation mentions the limit.
Options, in increasing order of effort: document the ceiling and return `413 Payload Too Large` instead of 500; store the state as compressed `BsonBinaryData`, which raises the practical ceiling without changing the data model; move state bodies to GridFS, which removes the limit entirely.
The last option interacts with B-15 and should be decided alongside it.

### H2. State values outside the BSON numeric range are corrupted or rejected

**Proven.**
The state is round-tripped through BSON, so JSON numbers are coerced into BSON numeric types, and the response is produced by `BsonDocument.ToJson()`, which emits MongoDB Extended JSON rather than the JSON that was stored.

Integers beyond `Int64` fail the write outright:

```bash
curl -u admin:admin123 -X POST http://localhost:5293/dummy/state/n1 \
  -H "Content-Type: application/json" --data '{"v":123456789012345678901234567890}'
```

The response is `500` with `System.OverflowException: Value was either too large or too small for an Int64`, thrown from `BsonDocument.Parse`.

Values beyond the `double` range are worse, because the write succeeds and the corruption is silent:

```bash
curl -u admin:admin123 -X POST http://localhost:5293/dummy/state/n2 \
  -H "Content-Type: application/json" --data '{"v":1e400}'   # 200 OK
curl -u admin:admin123 http://localhost:5293/dummy/state/n2
```

The read returns `{ "v" : { "$numberDouble" : "Infinity" } }`.
That is not the document that was written: a scalar became an object, and Terraform receives a state whose shape differs from the one it produced.

Numeric normalisation is visible on ordinary values as well: `1e3` reads back as `1000.0`, and `-0.0` reads back as `0.0`.
Those are semantically equivalent in JSON and are unlikely to break Terraform, but they confirm that the stored bytes are not the submitted bytes.

The narrower concern raised as M1 in the previous review is **withdrawn**: keys containing dots and keys prefixed with `$` round-trip correctly on MongoDB 8.2, verified with `{"tags":{"kubernetes.io/cluster":"x","$ref":"y"}}`.

The durable fix is to stop treating the state as a document to be parsed.
Storing the raw state string, and parsing a copy only when a diff is required, removes the whole class of problem and also removes the `Parse` failure path.
It is the same change as the one suggested for H1, which is why the two should be tackled together.

## Medium severity

### M1. A state POST without a `Content-Type` header returns 500

**Proven.**
This is a correction of finding M4 of the previous review, which was aimed at the wrong case.
`StateController.Create` carries `[Consumes("application/json", "text/json")]`, so a `text/plain` body is now rejected with `415 Unsupported Media Type` rather than the predicted 500.

A request with no `Content-Type` header at all behaves differently, because `ConsumesAttribute` does not reject a missing content type:

```bash
curl -u admin:admin123 -X POST http://localhost:5293/dummy/state/x \
  -H "Content-Type:" --data-binary '{"version":4}'
```

The body is bound by `RawRequestBodyFormatter` as a `string`, `JsonSerializer.Serialize` then double-encodes it into a quoted JSON string, and `BsonDocument.Parse` throws.
The response is `500`.

The finding has a second half that matters more than the bug.
Because `[Consumes]` restricts the state endpoints to JSON, `RawRequestBodyFormatter` is unreachable for every case it was written to serve, so it is effectively dead code that still creates one live failure path.
The cheapest correct fix is to delete the formatter and let a missing content type fall through to the JSON formatter.
Keeping it requires widening `[Consumes]` and handling the string case explicitly, which is more work for no known caller.
The exact content-type comparison in `CanRead` is a real defect, since `text/plain; charset=utf-8` is not matched, but it is moot if the formatter is removed.

### M2. `createdAt` in `tf_state` is overwritten on every update

**Proven.**
`StateRepository.CreateAsync` always writes `createdAt = DateTime.UtcNow`, so the field records the last update and the original creation time is lost.
After creating a state and updating it a second later, the stored `createdAt` is later than the `createdAt` of the history entry generated by that same update.
Keep the existing `createdAt` on update and add a separate `updatedAt` field.

### M3. Every state POST reads and re-parses the entire existing state

**Observed.**
`CreateAsync` fetches the whole existing document, calls `ToJson()` on it, computes a JSON Patch against the new state, and then performs two writes.
For a state anywhere near the ceiling described in H1 that is tens of megabytes of allocation, a full JSON parse, and a full structural diff on every single `terraform apply`.
The cost is paid whether or not anyone ever reads the history.

Making the history capture opt-in per tenant, or computing the diff outside the request path, would remove a large constant cost from the hot path.
This should be settled before B-15 and B-16 build more features on top of the current shape.

### M4. The state write and the history write are not atomic

**Observed.**
`CreateAsync` inserts the history entry and then replaces the state document, as two independent operations with no transaction and no compensating action.
A failure between them leaves a history entry describing a state transition that never happened.

This is worth recording now because the constraint that used to block it is gone: `compose.yaml` already starts MongoDB as a replica set (`rs0`), so multi-document transactions are available.
CI still installs a standalone `mongod`, which cannot run transactions, so aligning the CI database with the compose topology is a prerequisite for fixing this, and for B-15 generally.

### M5. BCrypt verification on every request, with no throttling

**Observed.**
Each authenticated request costs a BCrypt verify at work factor 10, roughly 50 to 100 ms of CPU.
There is no rate limiting, no failed-login lockout, and no short-lived credential cache, so the endpoint is an inexpensive CPU-exhaustion target and adds constant latency to every Terraform operation.

`UserRepository.CheckAuthentication` adds a second, smaller problem: `BCrypt.Verify` runs only when the username exists, so an unknown username returns in about a millisecond while a known one takes about a hundred.
That difference is a reliable username-enumeration oracle.
Verifying against a fixed dummy hash when the lookup misses equalises the two paths and costs one line.

### M6. Cancellation tokens are not propagated

**Observed.**
Controller actions and repository methods do not accept or forward `CancellationToken`, so aborted Terraform requests keep running their MongoDB queries.
Low effort to add, since `HttpContext.RequestAborted` flows naturally through the driver's async APIs.

## Low severity

### L1. The route name constraint is unanchored

**Proven.**
`{name:regex([[a-zA-Z]]+)}` requires one ASCII letter somewhere in the value rather than constraining the whole value.
The two cases are distinguishable by the response body, since an unmatched route returns an empty 404 and a matched route returns a problem detail:

```bash
curl -u admin:admin123 http://localhost:5293/dummy/state/12345        # 404, empty body: route rejected
curl -u admin:admin123 http://localhost:5293/dummy/state/123-abc-456  # 404, problem+json: route matched
```

Either anchor and widen it, for example `^[a-zA-Z][a-zA-Z0-9._-]*$`, or remove it and validate explicitly.
Anchoring it as it stands would break the scenario tests, which use names of the form `local-files-<guid>`, so widening is required rather than optional.

### L2. The authentication handler path workaround is redundant

**Proven.**
The `/scalar/` and `/openapi/` prefix check in `BasicAuthenticationHandler` duplicates what `AllowAnonymous` on those endpoints already achieves.
An anonymous `GET /scalar` returns 200 even though the check does not match the path, which demonstrates that `AllowAnonymous` is what actually grants access.
The check can be deleted, and `ScalarResourceTest` already covers the behaviour that must be preserved.

### L3. History entries are hard to consume

**Proven.**
`tf_state_history.upgrade` stores the JSON Patch as a string rather than a BSON document, for example `'[{"op":"replace","path":"/serial","value":2}]'`, and the field name does not convey "patch" or "diff".
Only the forward diff is kept, so reconstructing an older version requires replaying patches in the right direction with no API support.
This matters mostly for the planned history and revision features (see the [backlog](backlog.md)).

### L4. Version metadata is duplicated

**Observed.**
`OpenApi.Version` (`v1.2` in `appsettings.json`) must be kept in sync with `VersionPrefix` (`1.2.3` in `Directory.Build.props`) by hand.
Consider injecting the assembly version into the OpenAPI document at startup.

### L5. Unbounded growth and stale data

**Observed.**
`tf_state_history` has no TTL or retention policy, and locks never expire, so a crashed run requires a manual `terraform force-unlock`.
Both deserve at least documentation, and optionally a TTL index or a cleanup command in `tfbeadm`.

### L6. The MongoDB health check has no bounded timeout

**Observed.**
`MongoDbHealthCheck` forwards only the caller's cancellation token, and `AddCheck<MongoDbHealthCheck>("mongodb")` is registered without a timeout.
With default driver settings, server selection waits 30 seconds, so `/health` can hold a request open that long while MongoDB is unreachable.
The probe fails either way, so the impact is confined to resource use during an outage, which is when spare capacity matters most.
Registering the check with an explicit timeout bounds it in one line.

### L7. `StateModel` does not describe what is stored in `tf_state`

**Observed.**
`StateModel.CreatedAt` is mapped to `created_at`, but `StateRepository` writes `createdAt`, and the nested `StateValueModel` does not correspond to the shape of a real Terraform state either.
Nothing breaks today, because the model is only used by the test fakers, but it is a trap for the history and revision APIs in B-15 and B-16, which will be tempted to deserialise `tf_state` into it.
Either align the model with what is stored or delete it and move the faker to a test-local type.

### L8. The `Lock` endpoint can answer 423 where the protocol expects 409

**Observed.**
`StateController.Lock` returns `423` with a `{"message": ...}` body when a lock exists and the request body carries no ID, whereas every other lock conflict returns `409` with the existing lock as the body.
Terraform always sends an ID on lock, so the branch is not reachable through the CLI and the practical impact is nil.
It is noted only because the inconsistency is easy to mistake for intent when reading the controller.

## Test coverage

The integration suite covers the happy paths well: state create, read and delete, the full lock lifecycle including 423 and 409 responses, wrong-tenant 401, malformed `Authorization` headers, health, an OpenAPI snapshot, a Scalar feature-flag override, and a real `terraform init/plan/apply` scenario.
Three of the gaps listed in the previous review have been closed.

### T1. The test suite fails confusingly against a drifted local database

**Proven.**
`dotnet test` requires a MongoDB seeded by hand with an `admin` user on tenant `dummy`, and those values are hardcoded in `IntegrationTestBase`.
A first run of the suite for this review failed 6 of 13 tests, because the local `tfbackend_dev` database had accumulated an `admin` user belonging to a different tenant from unrelated work.
The reported symptom was an authentication failure, which points nowhere near the actual cause.
Re-running against a freshly seeded database passed 13 of 13.

Any long-lived development database drifts this way, and the failure mode does not distinguish "the code is broken" from "the database is stale".
That makes B-24, provisioning MongoDB through Testcontainers or compose, more valuable than its P3 ranking suggests, and it is raised to P2 in the backlog.

### T2. State fixtures are empty, which is why the fidelity bugs survived

**Observed.**
`StateFaker` is declared as `new Faker<StateModel>("en")` with no rules, so every state test posts a default-constructed `StateModel` rather than anything resembling a Terraform state.
No test has ever sent a number, a nested resource block, or a large payload through the storage layer.
That is the direct reason H1 and H2 went unnoticed: the round-trip is exercised, but only with values that cannot expose it.
A shared fixture built from a real state file, ideally captured from the `samples/` runs, would be a better foundation than the faker.

### T3. `IntegrationTestBase` mutates process-wide environment variables

**Observed.**
`CreateClient` calls `Environment.SetEnvironmentVariable("Features__IsScalarEnabled", "true")` on every invocation.
That value leaks across tests within the process and is never reset, so the suite depends on the fact that no test currently needs the opposite value from the environment.
`ScalarResourceTest` already demonstrates the cleaner mechanism, `UseSetting` on the host builder, which is scoped to one factory.

### Remaining gaps

- No concurrent lock-acquisition test, so the H3 fix of the previous review is unproven under the race it was written for.
- No test for a state POST without a `Content-Type` header (would have caught M1).
- No assertion that a state update writes a `tf_state_history` entry.
- No test for numeric edge cases or oversized states (would have caught H1 and H2).
- No unit tests, which is acceptable while the domain has no logic, but the diff logic in `StateRepository` is worth testing in isolation.

## CI/CD and operations

- The CI pipeline installs MongoDB and Terraform inline via `custom-commands`, and installs a standalone `mongod`, whereas `compose.yaml` runs a replica set.
  That divergence blocks any fix that needs a transaction from being tested in CI (see M4), and it is the main reason to prefer a `services:` container or the existing compose file.
- The Dockerfile is solid: SUSE BCI base images, a non-root user, checksum-verified OpenTelemetry auto-instrumentation, and no secrets baked in.
- `tfbeadm` is a pragmatic admin tool.
  It depends on `htpasswd` producing a `$2y$` BCrypt prefix, which `BCrypt.Net-Next` accepts.
  User creation interpolates values into a `mongosh` command through `printf %q`, which survives the double shell hop by relying on JavaScript treating `\$` as `$`.
  It works, but it works by coincidence rather than by design, and a password containing a quote would break it.
- The `demo` and `pkg` workflows were not reviewed in depth.

## Strengths worth preserving

- Strict protocol fidelity in `StateController`, documented in `AGENTS.md` so that future changes check the spec first.
- Scenario tests that exercise the real Terraform CLI end to end, which is what gives the NuGet verification above its weight.
- Central package management with transitive pinning, and a zero-CVE budget on image scans.
- Global MongoDB conventions (camelCase, string enums) registered in one place.
- The previous review's high-severity fixes were implemented cleanly, and `StateLockRepository.CreateAsync` in particular is a textbook insert-first duplicate-key mapping.

## Status of the previous review

Every finding of the 2026-07-10 review, re-checked against commit `d380eaa`.

Previous | Verdict | Now
-------- | ------------- | ---
H1 Health check does not verify MongoDB connectivity | Fixed | Closed, see L6 for a residual detail
H2 `Application__*` does not match `Features:*` | Fixed | Closed, verified in `compose.yaml` and CI
H3 Lock acquisition is not atomic | Fixed | Closed, though still untested under a real race
M1 State JSON round-trips through Extended JSON | **Escalated** | H2, proven to corrupt state and to return 500
M2 Malformed `Authorization` header returns 500 | Fixed | Closed, covered by a test
M3 `createdAt` is overwritten on every update | Confirmed | M2
M4 Raw `text/plain` state POST likely fails with 500 | **Corrected** | M1, `text/plain` returns 415; the real hole is a missing `Content-Type`
M5 BCrypt on every request, no throttling | Confirmed | M5, extended with a timing oracle
M6 Cancellation tokens are not propagated | Confirmed | M6
L1 Route name constraint is unanchored | Confirmed | L1, proven
L2 Authentication handler path workaround is redundant | Confirmed | L2, proven
L3 History entries are hard to consume | Confirmed | L3
L4 Version metadata is duplicated | Confirmed | L4
L5 Unbounded growth and stale data | Confirmed | L5

One claim from the previous review did not survive contact with the code.
The concern that `$`-prefixed and dotted keys might not round-trip is withdrawn: MongoDB 8.2 accepts both and returns them unchanged.
