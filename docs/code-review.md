# Code review

Assessment of version 1.3.1 as of 2026-11-12, covering source code, tests, CI/CD, container packaging and `tfbeadm`.
Identifiers are stable: the [backlog](backlog.md) refers to them.

## Method

A finding is **Proven** when a command or a test reproduced it against a running instance, and **Observed** when it comes from reading the code alone.
Reproduction commands are given inline so that each one can become a regression test.

## Overall assessment

The codebase is small, clean and focused, and in good health.
The three-project layering (`WebApi` > `Infrastructure.MongoDb` > `Domain`) holds, with one-way dependencies.
The Terraform HTTP backend protocol is implemented faithfully: raw `text/plain` state responses, `423 Locked`, `409 Conflict` with the current lock as body, and upsert on POST.
Tenant isolation is enforced by the tenant claim, by `TenantAuthorizationFilter`, and again by every repository query, and `TenantIsolationTest` covers both layers.

The conversion between the request payload and the stored BSON document is guarded:
out-of-range numbers are stored as `Decimal128`, the response is plain JSON rather than Extended JSON, and an oversized state answers `413` naming the limit.
The authentication path has a lockout shared across replicas that counts an attempt before verifying it,
a credential cache whose every hit reads the user again in the same request as the lockout counter, a constant-time miss,
and failure logs carrying the caller's address.
Responses carry `Cache-Control: no-store` and `X-Content-Type-Options: nosniff`, and no `Server` header.

The history diff runs after the state write returns, from `tf_state_history_queue`, one entry at a time per process, and a change whose estimated cost is above `History:MaxDiffComparisons` is set aside and reported rather than computed.
On a replica set, the state and its queue entry are written in one transaction.
The rest is small: the history of a deleted state (S11).
The platform controls are listed under [where each control belongs](#where-each-control-belongs).

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

### S11. A deleted state leaves its secrets in the history

**Observed.**
`StateRepository.DeleteAsync` removes the `tf_state` document only, so every diff of a deleted state, secrets included, stays in `tf_state_history` with no retention.
Keeping the history of a deleted state is a decision, so the exposure lasts until a retention policy exists (B-21).

### Where each control belongs

A control that needs only the connection, the request rate or the certificate belongs to the platform, which sees them before any application code runs and enforces them across every replica.
A control that needs the identity inside the request belongs to the application, since only the application decodes the Basic credential.

Control                                                      | Where       | Status
-------------------------------------------------------------|-------------|-------------------------------------------------------------------------------
Rate limit by source IP                                      | Platform    | Open, B-53
Pod reachable through the ingress alone                      | Platform    | Done, chart 0.3.4, off by default: `network.trustAllProxies` depends on it
TLS termination and HSTS                                     | Platform    | Documented in chart 0.3.4, a note in setup remains (B-42)
Mutual TLS                                                   | Platform    | Open, B-46
Encryption at rest, network isolation, database RBAC         | Platform    | Documented in chart 0.3.4, a note in setup remains (B-43)
Alerting on authentication-failure bursts                    | Platform    | Open, B-53: the `compose.yaml` OpenTelemetry collector is already the pipeline
Lockout after consecutive failures, per username and address | Application | Done, shared through `auth_lockout`, counted before the verify
Short-lived credential cache                                 | Application | Done, in process by design
Constant-time username miss                                  | Application | Done
Authentication failures logged with the source address       | Application | Done, `UseForwardedHeaders` running ahead of the authentication
Bound on the cost of the history diff                        | Application | Done, in the background, one at a time per process, above `History:MaxDiffComparisons` set aside
Tenant check ahead of the request body                       | Application | Done, `TenantAuthorizationFilter` is an authorization filter
MongoDB tracing kept out of the telemetry                    | Platform    | Done, chart 0.3.3
`tfbeadm` credential handling                                | Application | Done, except a minimum strength (B-44)

Rate limiting at the edge does not make a weak password safe, it only slows the attempts: a generated credential is what sets the difficulty of the guess.

## Medium severity

### M2. `created_at` in `tf_state` is overwritten on every update

**Proven.**
`StateRepository.CreateAsync` writes `created_at = DateTime.UtcNow` on every write, so the field records the last update and the creation time is lost.
This is by design: the document follows the Terraform data model, and an `updated_at` field is not added.

### M8. `BCrypt.Verify` runs synchronously on a thread-pool thread

**Observed.**
Every cold-cache request holds a thread-pool worker for about 139 ms of CPU.
`Task.Run` would not help, since the work is CPU-bound and would land on the same pool.
If this ever matters, the fix is a `SemaphoreSlim` bounding concurrent verifies.
No action is recommended: 100 concurrent failed authentications on a 22-core machine saturated nothing.

## Low severity

### L3. History entries are hard to consume

**Proven.**
`tf_state_history.upgrade` holds the JSON Patch as a string, the field name says neither "patch" nor "diff", and only the forward diff is kept,
so rebuilding an older version means replaying patches with no API support.
This is by design: a patch holds the change between two consecutive versions only, so it stays far below the document limit.

### L5. Unbounded growth and stale locks

**Observed.**
`tf_state_history` has no retention, deleting a state leaves its history behind by decision, and locks never expire.
A single-document write is atomic, so an interrupted run never leaves a half-written state: it leaves a lock nothing releases, which needs `terraform force-unlock` (B-18, B-21).

## Test coverage

93 tests: 34 unit tests, and 59 integration tests against a real MongoDB and, for the scenarios, the real `terraform` CLI.
Line coverage of the handwritten code under `src/` is 96.9% (631 of 651 lines), measured with the collector CI uses and excluding the OpenAPI source generator's output.
The least covered files are `StateController.cs` (89%) and `BasicAuthenticationHandler.cs` (91%).

`GenerateJsonDiff` in `StateRepository` is private and reached only through the integration suites.

## Code comments

Comments follow the repository's writing style: a comment says why, and reads as if the code had always been so.
The test suite still names review identifiers in a few comments and summaries, which is intended: a regression test names the finding it guards.

## Dependencies

Every package is on its latest release, with one deliberate exception.

- `SharpCompress` is pinned transitively on 0.50.x, since `MongoDB.Driver` depends on the 0.x line and 1.0 is a breaking release.

## CI/CD and operations

- CI starts MongoDB through the workflow parts as the single-node replica set `rs0`, as `compose.yaml` does, so CI runs the transaction of a state write, and installs Terraform inline through `custom-commands`.
  The suite creates its own indexes, so CI seeds nothing.
- The Dockerfile is solid: SUSE BCI base images, a non-root user, checksum-verified OpenTelemetry auto-instrumentation, and no secrets baked in.
- `tfbeadm` passes every caller-supplied value through the environment, never through interpolation, and reads the password from a prompt or standard input.
- The `demo` and `pkg` workflows were not reviewed in depth.

## Strengths worth preserving

- Strict protocol fidelity in `StateController`.
- Scenario tests that drive the real Terraform CLI end to end, and a suite that proves it left the database as it found it.
- Central package management with transitive pinning, and a zero-CVE budget on image scans.
- `StateLockRepository.CreateAsync` and `LockoutRepository.RecordFailureAsync`: atomicity from a unique index and from a single pipeline update, rather than from a read followed by a write.
