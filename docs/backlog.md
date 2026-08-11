# Backlog

Prioritized list of missing or improvable items, consolidated from the [code review](code-review.md) (2026-08-11), the existing V2 ideas in [project](project.md), and new feature requests.
Priorities: **P1** = should be fixed before the next release, **P2** = planned, **P3** = nice to have.
Size: S (hours), M (days), L (weeks).

## Storage fidelity

The 2026-08-11 review found that the conversion between the request payload and the stored document is unguarded.
Storing the state as a queryable BSON document is a fixed requirement, since other applications read `tf_state` directly, so every item below preserves it.
The shape of a `tf_state` document is a published contract and will not change, so solutions that make the state opaque (GridFS, compressed binary, a raw JSON string) and solutions that restructure it (splitting `resources` into separate documents) are both ruled out.
The 16 MB document limit is therefore accepted as permanent, and the work is to fail cleanly at it.

ID   | Item                                                                                                             | Priority | Size | Origin
---- | ---------------------------------------------------------------------------------------------------------------- | -------- | ---- | ---------
B-05 | Parse out-of-range JSON numbers into `Decimal128`, and render non-finite `Double` and `Decimal128` as plain JSON  | P1       | S    | Review H2
B-27 | Return `413` and a documented limit instead of an unhandled `500` when a state exceeds the BSON document size | P1       | S    | Review H1
B-28 | Store history patches as BSON documents and rename the `upgrade` field, so they stop hitting the limit first      | P2       | S    | Review H1, L3
B-29 | Stop reading and re-parsing the full state on every POST: make history capture opt-in or move it off the request  | P2       | M    | Review M3

## Reliability and correctness

ID   | Item                                                                                                                    | Priority | Size | Origin
---- | ----------------------------------------------------------------------------------------------------------------------- | -------- | ---- | ---------
B-01 | Add a MongoDB ping to the health check so `/health` reflects database availability (done 2026-07-10)                    | Done     | S    | Review H1 (2026-07-10)
B-02 | Fix the `Application__*` vs `Features:*` configuration key mismatch and test that env overrides apply (done 2026-07-10) | Done     | S    | Review H2 (2026-07-10)
B-03 | Make lock acquisition atomic: insert first, map duplicate-key errors to `409 Conflict` (done 2026-07-10)                | Done     | S    | Review H3 (2026-07-10)
B-04 | Return 401 instead of 500 on malformed `Authorization` headers (done 2026-07-10)                                        | Done     | S    | Review M2 (2026-07-10)
B-06 | Delete `RawRequestBodyFormatter`, which `[Consumes]` makes unreachable while leaving a 500 on a missing content type    | P2       | S    | Review M1
B-30 | Align CI on the compose replica-set topology, so transactions can be tested                                             | P2       | S    | Review M4
B-31 | Make the state write and the history write atomic, once B-30 lands                                                      | P2       | S    | Review M4
B-07 | Preserve `createdAt` on updates and add an `updatedAt` field in `tf_state`                                              | P3       | S    | Review M2
B-08 | Propagate `CancellationToken` from controllers to the MongoDB driver                                                    | P3       | S    | Review M6
B-09 | Anchor and widen the route `name` regex constraint, or remove it                                                        | P3       | S    | Review L1
B-10 | Remove the redundant Scalar/OpenAPI path workaround in `BasicAuthenticationHandler`                                     | P3       | S    | Review L2
B-32 | Bound the MongoDB health check with an explicit timeout                                                                 | P3       | S    | Review L6
B-33 | Align `StateModel` with what `tf_state` actually stores, or delete it and move the faker to a test-local type           | P3       | S    | Review L7

## Security

ID   | Item                                                                                        | Priority | Size | Origin
---- | ------------------------------------------------------------------------------------------- | -------- | ---- | ---------
B-11 | Rate limiting and/or failed-authentication lockout, plus optional credential result caching | P2       | M    | Review M5
B-34 | Close the username-enumeration timing oracle by verifying a dummy hash on lookup miss       | P3       | S    | Review M5
B-12 | Request body size limits and payload validation on state and lock endpoints                 | P3       | S    | Review
B-13 | Document secret management options (mounted secret files, Key Vault/Secrets Manager)        | P3       | S    | New

## Features and API

ID   | Item                                                                                                                             | Priority | Size | Origin
---- | -------------------------------------------------------------------------------------------------------------------------------- | -------- | ---- | --------------------
B-14 | Capture caller run context (git repo, branch, dirty flag, environment), see the [feasibility study](feasibility-run-context.md)   | P2       | M    | New feature request
B-15 | Store only the latest state in `tf_state` and previous versions in `tf_state_revision`                                           | P2       | M    | Project V2
B-16 | State history API: list revisions, fetch a revision, reconstruct a past state                                                    | P2       | M/L  | Project V2 follow-up
B-17 | Administrative API: list states per tenant, delete tenant data, manage users (replacing `tfbeadm`-only)                          | P3       | M    | New
B-18 | Stale lock handling: surface lock age, optional TTL or `tfbeadm` cleanup command, force-unlock runbook                           | P3       | S/M  | Review L5

## Observability

ID   | Item                                                                                      | Priority | Size | Origin
---- | ----------------------------------------------------------------------------------------- | -------- | ---- | ----------
B-19 | OpenTelemetry SDK-native instrumentation with custom spans (including MongoDB operations) | P2       | M    | Project V2
B-20 | Structured audit log for every state and lock operation (tenant, name, user, outcome)     | P3       | S    | New

## Data management

ID   | Item                                                                   | Priority | Size | Origin
---- | ---------------------------------------------------------------------- | -------- | ---- | ---------
B-21 | Retention/TTL policy for `tf_state_history`                            | P2       | S    | Review L5

## Tests and developer experience

ID   | Item                                                                                                            | Priority | Size | Origin
---- | ----------------------------------------------------------------------------------------------------------------- | -------- | ---- | ---------
B-24 | Testcontainers (or compose-based) MongoDB provisioning so `dotnet test` needs no manual setup                    | P2       | M    | Review T1
B-35 | Replace the empty `StateFaker` with a realistic state fixture captured from a `samples/` run                     | P2       | S    | Review T2
B-23 | Close test gaps: concurrent locks, missing `Content-Type`, history assertions, numeric edge cases, oversized state | P2       | M    | Review
B-36 | Stop mutating process-wide environment variables in `IntegrationTestBase`, use `UseSetting` instead              | P3       | S    | Review T3
B-25 | Align the OpenAPI document version with `VersionPrefix` automatically                                            | P3       | S    | Review L4
B-26 | Add a `CHANGELOG.md` or automated release notes                                                                  | P3       | S    | New
