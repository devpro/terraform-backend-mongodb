# Code review

Assessment of version 1.3.0 as of 2026-09-24, covering source code, tests, CI/CD, container packaging and `tfbeadm`.
Identifiers are stable: the [backlog](backlog.md) refers to them, and a fixed finding keeps its identifier in the [fixed findings](#fixed-findings) table.

## Method

A finding is **Proven** when a command or a test reproduced it against a running instance, and **Observed** when it comes from reading the code alone.
Reproduction commands are given inline so that each one can become a regression test.

## Overall assessment

The codebase is small, clean and focused, and in good health.
The three-project layering (`WebApi` > `Infrastructure.MongoDb` > `Domain`) holds, with one-way dependencies.
The Terraform HTTP backend protocol is implemented faithfully: raw `text/plain` state responses, `423 Locked`, `409 Conflict` with the current lock as body, and upsert on POST.
Tenant isolation is enforced by the tenant claim, by `TenantAuthorizationFilter`, and again by every repository query, and `TenantIsolationTest` covers both layers.

The conversion between the request payload and the stored BSON document is guarded: out-of-range numbers are stored as `Decimal128`, the response is plain JSON rather than Extended JSON, and an oversized state answers `413` naming the limit.
The authentication path has a lockout shared across replicas, a credential cache, a constant-time miss, and failure logs carrying the caller's address.

What remains open is small: two costs paid on every state write, the non-atomic history write, and questions that need a decision on the data model.
The largest remaining exposure is outside the code, in the platform controls listed under [where each control belongs](#where-each-control-belongs).

## Security

The Terraform `http` backend authenticates with a Basic credential sent on every request, and offers no token, no OAuth flow and no custom headers.
So a long-lived credential travels on every request, and the server pays to verify it every time.
The backend does support mutual TLS (`client_certificate_pem`, `client_private_key_pem`, `client_ca_certificate_pem`), the only Terraform-native second factor.

### S4. Basic credentials depend entirely on transport security

**Observed.**
The container serves plain HTTP on 8080, TLS terminates at an ingress outside this repository, and HSTS is configured nowhere.
Since the password is replayed on every request, one plaintext hop exposes a credential that unlocks every workspace of the tenant.
The deployment documentation should state that TLS termination is mandatory, and that `skip_cert_verification` in a backend block defeats it.

### S5. Terraform state is a secret store, and `tf_state` holds it in the clear

**Observed.**
This follows from the storage contract rather than from a defect.
A Terraform state holds provider credentials, generated passwords and private keys in plaintext, and other applications, `liveship` among them, read `tf_state` directly.
The blast radius of the database is therefore every secret of every managed workspace.
The controls this makes necessary are encryption at rest, a least-privilege read-only user for consuming applications, network isolation, and treating a `tf_state` backup as a secret.

### Where each control belongs

A control that needs only the connection, the request rate or the certificate belongs to the platform, which sees them before any application code runs and enforces them across every replica.
A control that needs the identity inside the request belongs to the application, since only the application decodes the Basic credential.

Control | Where | Status
------- | ----- | ------
Rate limit by source IP | Platform | Open, B-53
TLS termination and HSTS | Platform | Open, B-42
Mutual TLS | Platform | Open, B-46
Encryption at rest, network isolation, database RBAC | Platform | Open, B-43
Alerting on authentication-failure bursts | Platform | Open, B-53: the `compose.yaml` OpenTelemetry collector is already the pipeline
Lockout after consecutive failures, per username and address | Application | Done, shared through `auth_lockout`
Short-lived credential cache | Application | Done, in process by design
Constant-time username miss | Application | Done
Authentication failures logged with the source address | Application | Done, relying on `UseForwardedHeaders`
`tfbeadm` credential handling | Application | Done, except a minimum strength (B-44)

Rate limiting at the edge does not make a weak password safe, it only slows the attempts: a generated credential is what sets the difficulty of the guess.

## Medium severity

### M2. `created_at` in `tf_state` is overwritten on every update

**Proven.**
`StateRepository.CreateAsync` writes `created_at = DateTime.UtcNow` on every write, so the field records the last update and the creation time is lost.
Fixing it adds an `updated_at` field, which changes the data model and waits on the maintainer (B-07).

### M3. Every state POST reads and re-parses the entire existing state

**Observed.**
`CreateAsync` reads the whole existing document, renders it to JSON, diffs it against the new state, and performs two writes.
Near the 16 MB ceiling that is tens of megabytes of allocation and a full structural diff on every `terraform apply`, whether or not anyone reads the history (B-29).

### M4. The state write and the history write are not atomic

**Observed.**
The history entry is inserted, then the state is replaced, with no transaction.
A failure between the two leaves a history entry for a transition that never happened.
`compose.yaml` runs a replica set, so transactions are available, but CI runs a standalone `mongod`, which cannot test one (B-30, B-31).
Cancellation is therefore honoured only by the read that precedes the two writes, so an aborted request cannot widen the gap.

### M8. `BCrypt.Verify` runs synchronously on a thread-pool thread

**Observed.**
Every cold-cache request holds a thread-pool worker for about 139 ms of CPU.
`Task.Run` would not help, since the work is CPU-bound and would land on the same pool.
If this ever matters, the fix is a `SemaphoreSlim` bounding concurrent verifies.
No action is recommended: 100 concurrent failed authentications on a 22-core machine saturated nothing.

## Low severity

### L3. History entries are hard to consume

**Proven.**
`tf_state_history.upgrade` holds the JSON Patch as a string, the field name says neither "patch" nor "diff", and only the forward diff is kept, so rebuilding an older version means replaying patches with no API support.
Changing it changes the data model (B-28).

### L5. Unbounded growth and stale locks

**Observed.**
`tf_state_history` has no retention, deleting a state leaves its history behind (B-50), and locks never expire.
A single-document write is atomic, so an interrupted run never leaves a half-written state: it leaves a lock nothing releases, which needs `terraform force-unlock` (B-18, B-21).

## Test coverage

82 tests: 30 unit tests, and 52 integration tests against a real MongoDB and, for the scenarios, the real `terraform` CLI.
Line coverage of the handwritten code under `src/` is 96.9% (631 of 651 lines), measured with the collector CI uses and excluding the OpenAPI source generator's output.
The least covered files are `StateController.cs` (89%) and `BasicAuthenticationHandler.cs` (91%).

`GenerateJsonDiff` in `StateRepository` is private and reached only through the integration suites.

## Code comments

Comments follow the [writing style](../AGENTS.md#writing-style): a comment says why, and reads as if the code had always been so.
The test suite still names review identifiers in a few comments and summaries, which is intended: a regression test names the finding it guards.

## Dependencies

Every package is on its latest release, with two deliberate exceptions.

- `Verify.XunitV3` stays on 32.x: from 33.0 its build fails until a sponsorship or licence-exemption property is declared, which is the maintainer's call to make.
- `SharpCompress` is pinned transitively on 0.50.x, since `MongoDB.Driver` depends on the 0.x line and 1.0 is a breaking release.

## CI/CD and operations

- CI installs MongoDB and Terraform inline through `custom-commands`, as a standalone `mongod`, whereas `compose.yaml` runs a replica set (M4, B-30).
  The suite creates its own indexes, so CI seeds nothing.
- The Dockerfile is solid: SUSE BCI base images, a non-root user, checksum-verified OpenTelemetry auto-instrumentation, and no secrets baked in.
- `tfbeadm` passes every caller-supplied value through the environment, never through interpolation, and reads the password from a prompt or standard input.
- The `demo` and `pkg` workflows were not reviewed in depth.

## Strengths worth preserving

- Strict protocol fidelity in `StateController`, with the constraints written down in `AGENTS.md`.
- Scenario tests that drive the real Terraform CLI end to end, and a suite that proves it left the database as it found it.
- Central package management with transitive pinning, and a zero-CVE budget on image scans.
- `StateLockRepository.CreateAsync` and `LockoutRepository.RecordFailureAsync`: atomicity from a unique index and from a single pipeline update, rather than from a read followed by a write.

## Fixed findings

ID | Finding | Covered by
-- | ------- | ----------
H1 | A state larger than 16 MB answered 500; it now answers `413` naming the limit | `StateFidelityTest`
H2 | Numbers outside the BSON range were rejected or read back as Extended JSON | `StateFidelityTest`, `JsonToBsonConverterTest`, `BsonToJsonConverterTest`
S1 | Unlimited authentication attempts, with no lockout and no record | `AuthenticationLockoutTest`
S2 | BCrypt on every request was a CPU-exhaustion lever | `AuthenticationLockoutTest`
S3 | Response time revealed which usernames exist | `AuthenticationTimingTest`
S6 | `tfbeadm` leaked the password and allowed JavaScript injection through the username | `TfbeadmTest`
S7 | Authentication thresholds had no startup validation | `AuthenticationServiceCollectionExtensionsTest`
M7 | The lockout was scoped to one replica | `LockoutRepositoryTest`
L9 | The MongoDB connection pool size was undocumented | [setup](setup.md#database-server)
T1 | The suite failed confusingly against a drifted development database | `TestDatabaseFixture`, `TestDatabaseGuard`
T2 | Nothing asserted what comes back out of storage | `StateFidelityTest`, `ComplexStateScenarioTest`
T3 | Tests mutated process-wide environment variables | `TestHostConfiguration`
S8 | An unparseable trusted proxy silently disabled forwarded headers; it now fails at startup | `AuthenticationServiceCollectionExtensionsTest`
M1 | `RawRequestBodyFormatter` served no client, since Terraform and OpenTofu always send `application/json`; it is deleted, and a POST without `Content-Type` answers 415 rather than 400 | `StateControllerResourceTest`
M6 | Cancellation tokens were not propagated; state and lock operations take the request's token, the authentication path deliberately does not, so a disconnect after a wrong guess is still counted | Compiler
L1 | The route name constraint was unanchored; it is removed, since it validated nothing | `StateControllerResourceTest`, OpenAPI snapshot
L2 | The authentication handler duplicated `AllowAnonymous` with a path check | `ScalarResourceTest`, `OpenApiResourceTest`
L4 | The OpenAPI version was kept in step with `VersionPrefix` by hand; it is derived from the assembly | OpenAPI snapshot
L6 | The health check had no timeout; it is bounded at 30 seconds, which a cold Atlas cluster can need | `HealthCheckResourceTest`
L7 | `StateModel` and `StateValueModel` did not describe `tf_state`; both are deleted, and tests build a minimal state | Compiler
L8 | The `Lock` endpoint answered an empty `ID` with `423` and a message body, which Terraform cannot read as lock info; a conflicting lock always answers `409` with the holding lock | `StateControllerResourceTest`, OpenAPI snapshot
L10 | A number beyond 34 significant digits was refused as out of range; the message names the precision | `JsonToBsonConverterTest`
L11 | CI created indexes in a database the suite does not use | CI
L12 | The lock ID was the `tf_state_lock` `_id`, so a reused ID collided across states and answered `409` with the caller's own lock; it is a `lock_id` field, unique per `{tenant, name}`, and `tfbeadm migrate-lock-id` moves existing locks | `StateControllerResourceTest`, `MigrateLockIdTest`
