# Backlog

Prioritized list of missing or improvable items, consolidated from the [code review](code-review.md) (2026-07-10), the existing V2 ideas in [project](project.md), and new feature requests.
Priorities: **P1** = should be fixed before the next release, **P2** = planned, **P3** = nice to have.
Size: S (hours), M (days), L (weeks).

## Reliability and correctness

ID   | Item                                                                                                                    | Priority | Size | Origin
---- | ----------------------------------------------------------------------------------------------------------------------- | -------- | ---- | ---------
B-01 | Add a MongoDB ping to the health check so `/health` reflects database availability (done 2026-07-10)                    | Done     | S    | Review H1
B-02 | Fix the `Application__*` vs `Features:*` configuration key mismatch and test that env overrides apply (done 2026-07-10) | Done     | S    | Review H2
B-03 | Make lock acquisition atomic: insert first, map duplicate-key errors to `409 Conflict` (done 2026-07-10)                | Done     | S    | Review H3
B-04 | Return 401 instead of 500 on malformed `Authorization` headers (done 2026-07-10)                                        | Done     | S    | Review M2
B-05 | Guarantee byte-level JSON fidelity of state round-trips (explicit writer settings or raw string storage)                | P2       | M    | Review M1
B-06 | Fix or explicitly reject `text/plain` state POSTs (double-encoding path) and handle content-type params                 | P2       | S    | Review M4
B-07 | Preserve `createdAt` on updates and add an `updatedAt` field in `tf_state`                                              | P3       | S    | Review M3
B-08 | Propagate `CancellationToken` from controllers to the MongoDB driver                                                    | P3       | S    | Review M6
B-09 | Anchor or remove the route `name` regex constraint                                                                      | P3       | S    | Review L1
B-10 | Remove the redundant Scalar/OpenAPI path workaround in `BasicAuthenticationHandler`                                     | P3       | S    | Review L2

## Security

ID   | Item                                                                                        | Priority | Size | Origin
---- | ------------------------------------------------------------------------------------------- | -------- | ---- | ---------
B-11 | Rate limiting and/or failed-authentication lockout, plus optional credential result caching | P2       | M    | Review M5
B-12 | Request body size limits and payload validation on state and lock endpoints                 | P3       | S    | Review
B-13 | Document secret management options (mounted secret files, Key Vault/Secrets Manager)        | P3       | S    | New

## Features and API

ID   | Item                                                                                                                             | Priority | Size | Origin
---- | -------------------------------------------------------------------------------------------------------------------------------- | -------- | ---- | --------------------
B-14 | Capture caller run context (git repo, branch, dirty flag, environment) — see the [feasibility study](feasibility-run-context.md) | P2       | M    | New feature request
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
B-22 | Store history patches as BSON documents and rename the `upgrade` field | P3       | S    | Review L3

## Tests and developer experience

ID   | Item                                                                                                       | Priority | Size | Origin
---- | ---------------------------------------------------------------------------------------------------------- | -------- | ---- | ---------
B-23 | Close test gaps: malformed auth, concurrent locks, `text/plain` POST, history assertions, config overrides | P2       | M    | Review
B-24 | Testcontainers (or compose-based) MongoDB provisioning so `dotnet test` needs no manual setup              | P3       | M    | New
B-25 | Align the OpenAPI document version with `VersionPrefix` automatically                                      | P3       | S    | Review L4
B-26 | Add a `CHANGELOG.md` or automated release notes                                                            | P3       | S    | New
