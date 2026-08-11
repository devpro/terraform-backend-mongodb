# Backlog

What needs to change, and why.
Sourced from the [code review](code-review.md), the V2 ideas in [project](project.md), and new feature requests.
Priorities: **P1** = before the next release, **P2** = planned, **P3** = nice to have.
Size: S (hours), M (days), L (weeks).

Completed items are not listed.
The 2026-07-10 fixes are recorded in the review's status table and in the git history.
Identifiers are stable references, not an ordering.

## Fixed constraints

Two constraints rule out whole classes of solution, so they are stated once rather than repeated per item.

The shape of a `tf_state` document is a published contract: other applications query it by resource attribute, and it will not change.
That rules out making the state opaque (GridFS, compressed binary, a raw JSON string) and rules out restructuring it (splitting `resources` into separate documents).
The 16 MB BSON document limit is therefore permanent, and the work is to fail cleanly at it rather than to escape it.

Improvements belong in the .NET layer, not in the stored representation.

## Correctness

ID   | Change | Why | Priority | Size
---- | ------ | --- | -------- | ----
B-05 | Parse out-of-range JSON numbers into `Decimal128`, and render non-finite `Double` and `Decimal128` as plain JSON | A number beyond `Int64` returns 500, and `1e400` is stored and returned as a different shape from the one Terraform wrote | P1 | S
B-27 | Return `413` with the limit named, and document it | A state above the BSON limit currently fails as an unhandled 500 in the middle of an apply | P1 | S
B-06 | Delete `RawRequestBodyFormatter` | `[Consumes]` makes it unreachable for every case it was written for, while still leaving a 500 on a request with no `Content-Type` | P2 | S
B-28 | Store history patches as BSON documents and rename the `upgrade` field | The string-encoded patch reaches the document limit before the state does, and the field name does not say what it holds | P2 | S
B-29 | Stop reading and re-parsing the whole state on every POST | Every apply pays a full read, parse and diff, whether or not the history is ever read | P2 | M
B-30 | Run CI against a replica set, as `compose.yaml` already does | CI cannot run transactions today, which blocks B-31 and B-15 | P2 | S
B-31 | Make the state write and the history write atomic | A failure between them leaves a history entry describing a transition that never happened | P2 | S
B-07 | Keep `createdAt` on update and add `updatedAt` | `createdAt` currently records the last update, so the creation time is lost | P3 | S
B-08 | Propagate `CancellationToken` to the driver | An aborted Terraform request keeps its MongoDB query running | P3 | S
B-09 | Widen and anchor the route `name` constraint, or remove it | It requires one letter anywhere in the value, so it neither validates nor documents anything | P3 | S
B-10 | Remove the Scalar and OpenAPI path check in `BasicAuthenticationHandler` | `AllowAnonymous` already grants access, and the check is bypassed by `/scalar` without a trailing slash | P3 | S
B-32 | Bound the MongoDB health check with a timeout | `/health` can hold a request for the full server selection timeout while the database is down | P3 | S
B-33 | Align `StateModel` with what `tf_state` stores, or delete it | It maps `created_at` while the repository writes `createdAt`, which is a trap for B-15 and B-16 | P3 | S

## Tests

The suite does reach the storage layer, including a real `terraform apply` in the scenario test.
The gap is narrower than that: nothing asserts what came back out, and the payload never varies.

ID   | Change | Why | Priority | Size
---- | ------ | --- | -------- | ----
B-23 | Assert that the state returned by GET equals the state that was POSTed, and drive that test from a table of payloads including numeric edge cases | Nothing compares input to output today, and the sample only ever produces strings and small integers, so no test can observe a fidelity loss | P2 | S
B-38 | Add a concurrent lock-acquisition test | The 2026-07-10 atomicity fix is unproven under the race it was written for | P2 | S
B-39 | Assert that a state update writes a `tf_state_history` entry | The history path has no coverage at all | P3 | S
B-36 | Replace the process-wide environment variable in `IntegrationTestBase` with `UseSetting` | The value leaks across tests and is never reset, so the suite is order-dependent | P3 | S

## Security

ID   | Change | Why | Priority | Size
---- | ------ | --- | -------- | ----
B-11 | Rate limiting or failed-authentication lockout, with optional credential caching | A BCrypt verify runs on every request, which is a cheap CPU-exhaustion target and adds latency to every Terraform operation | P2 | M
B-34 | Verify a dummy hash when the username lookup misses | The timing difference between a known and an unknown username is a reliable enumeration oracle | P3 | S
B-12 | Request body size limits and payload validation | Nothing bounds a request before it reaches the driver | P3 | S
B-13 | Document secret management options | Deployments have no guidance on mounted secret files or a secrets manager | P3 | S

## Features

ID   | Change | Why | Priority | Size
---- | ------ | --- | -------- | ----
B-14 | Capture caller run context, see the [feasibility study](feasibility-run-context.md) | Requested, and there is no way to tell which repository or branch produced a state | P2 | M
B-15 | Keep the latest state in `tf_state` and previous versions in `tf_state_revision` | Planned for V2, and the current forward-only patch history cannot reconstruct a past state | P2 | M
B-16 | State history API: list revisions, fetch one, reconstruct a past state | The history is written but there is no way to read it | P2 | M/L
B-19 | OpenTelemetry SDK-native instrumentation with custom spans | Auto-instrumentation gives no visibility into MongoDB operations | P2 | M
B-17 | Administrative API for states, tenants and users | Administration is only possible through `tfbeadm` | P3 | M
B-18 | Stale lock handling: lock age, optional TTL, force-unlock runbook | A crashed run leaves a lock that nothing will ever release | P3 | S/M
B-20 | Structured audit log for every state and lock operation | There is no record of who changed what | P3 | S

## Operations and developer experience

ID   | Change | Why | Priority | Size
---- | ------ | --- | -------- | ----
B-24 | Provision MongoDB for tests through Testcontainers or compose | The suite depends on a hand-seeded database, and against a drifted one it fails with an authentication error that points nowhere near the cause | P2 | M
B-21 | Retention or TTL policy for `tf_state_history` | The collection grows without bound | P2 | S
B-25 | Derive the OpenAPI document version from `VersionPrefix` | Two version numbers are kept in sync by hand | P3 | S
B-26 | Add a `CHANGELOG.md` or automated release notes | Releases have no record of what changed | P3 | S
