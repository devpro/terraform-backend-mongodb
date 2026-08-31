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

The data model is a published contract: other applications query it by resource attribute, and it will not change.
`tf_state` holds the latest version only, one document per `{tenant, name}`, and `tf_state_history` holds the computed change between one version and the next.
That rules out making the state opaque (GridFS, compressed binary, a raw JSON string) and rules out restructuring it (splitting `resources` into separate documents).
The 16 MB BSON document limit is therefore permanent, and the work is to fail cleanly at it rather than to escape it.

Improvements belong in the .NET layer, not in the stored representation.
The model changes only when the maintainer specifically asks for it, and an item in this backlog is not such a request.

Three items below would change it and are therefore **blocked pending confirmation**, marked `needs decision` rather than removed, because each solves a real problem and the call is the maintainer's:

- **B-07** adds an `updatedAt` field to `tf_state`.
- **B-28** changes the `tf_state_history` document: the patch stored as BSON rather than as a string, and the `upgrade` field renamed.
- **B-15** introduces a `tf_state_revision` collection, which conflicts directly with `tf_state` holding the latest version only. It came from the V2 ideas in [project](project.md) and predates the rule.

The second constraint is the client.
Terraform's `http` backend sends a Basic credential on every request and supports no bearer token, no OAuth flow and no custom headers, so the authentication scheme cannot be replaced.
It does support mutual TLS (`client_certificate_pem`, `client_private_key_pem`, `client_ca_certificate_pem`), which is the only Terraform-native way to add a second factor.

## Security

Ranked first because the application is in daily production use and its database holds, in the clear, every secret of every managed workspace.
Sourced from the [security section](code-review.md#security) of the code review.

The application-side core is done: the failed-attempt lockout, the credential cache, the dummy-hash verify that closes the enumeration oracle, authentication failures logged with the caller's address, and the trusted-proxy configuration those two depend on, all now validated at startup and documented.
What remains is mostly the platform's half.

Most of this belongs to the platform rather than to C#, and the [split is set out in the review](code-review.md#where-each-control-belongs).
The `Where` column below records the outcome, so that no item is built twice or assumed to be somebody else's.
The platform items land in the Helm chart, which lives in `devpro/helm-charts` rather than here, so each one needs an issue in that repository and a matching note in [setup](setup.md).

ID   | Change | Why | Where | Priority | Size
---- | ------ | --- | ----- | -------- | ----
B-53 | Rate limit by source IP at the ingress, and alert on authentication-failure bursts through the existing collector | The edge rejects a request before any CPU is spent on it and holds one counter across every replica, which an in-process limiter cannot | Platform | P1 | S
B-42 | Document that TLS termination is mandatory, and add HSTS | The password is replayed on every request, so one plaintext hop exposes every workspace in the tenant, and `skip_cert_verification` silently defeats the protection it appears to configure | Platform | P1 | S
B-43 | Document the database as a secret-bearing store: encryption at rest, a least-privilege read-only user for consuming applications, network isolation, and backups treated as secrets | A Terraform state holds provider credentials and private keys in plaintext, and the storage contract makes `tf_state` directly readable by other applications | Platform | P1 | S
B-44 | Enforce a minimum strength on a credential created by `tfbeadm` | The prompt and the generated-credential guidance are in place, but nothing stops a weak password being accepted, and no rate limit at the edge makes one safe | App | P3 | S
B-13 | Document secret management options for the deployment itself | Deployments have no guidance on mounted secret files or a secrets manager for the MongoDB connection string | Platform | P3 | S
B-46 | Validate a client certificate at the ingress and pass the verified subject to the application | It is the only second factor the Terraform client can offer, and the ingress already owns certificate distribution and revocation | Platform | P3 | M
B-12 | Lower the request body size limit below the Kestrel default of 30 MB | The default sits above the 16 MB BSON ceiling, so an oversized state reaches the driver and fails as a 500 rather than being rejected at the edge | App | P3 | S
B-20 | Structured audit log for every state and lock operation | There is no record of who changed what | App | P3 | S
B-55 | Share the failed-attempt lockout and credential cache across replicas (**needs decision**, backing store) | Effective lockout budget and cache hit rate both divide by replica count once more than one replica runs behind a load balancer, see [M7](code-review.md#m7-the-failed-attempt-lockout-and-credential-cache-are-scoped-to-one-replica) | App | P3 | M

## Correctness

ID   | Change | Why | Priority | Size
---- | ------ | --- | -------- | ----
B-06 | Delete `RawRequestBodyFormatter` | `[Consumes]` makes it unreachable for every case it was written for, while still leaving a 500 on a request with no `Content-Type` | P2 | S
B-28 | Store history patches as BSON documents and rename the `upgrade` field (**needs decision**, changes the data model) | The string-encoded patch reaches the document limit before the state does, and the field name does not say what it holds | P2 | S
B-29 | Stop reading and re-parsing the whole state on every POST | Every apply pays a full read, parse and diff, whether or not the history is ever read | P2 | M
B-30 | Run CI against a replica set, as `compose.yaml` already does | CI cannot run transactions today, which blocks B-31 and B-15 | P2 | S
B-31 | Make the state write and the history write atomic | A failure between them leaves a history entry describing a transition that never happened | P2 | S
B-07 | Keep `createdAt` on update and add `updatedAt` (**needs decision**, changes the data model) | `createdAt` currently records the last update, so the creation time is lost | P3 | S
B-08 | Propagate `CancellationToken` to the driver | An aborted Terraform request keeps its MongoDB query running | P3 | S
B-09 | Widen and anchor the route `name` constraint, or remove it | It requires one letter anywhere in the value, so it neither validates nor documents anything | P3 | S
B-10 | Remove the Scalar and OpenAPI path check in `BasicAuthenticationHandler` | `AllowAnonymous` already grants access, and the check is bypassed by `/scalar` without a trailing slash | P3 | S
B-32 | Bound the MongoDB health check with a timeout | `/health` can hold a request for the full server selection timeout while the database is down | P3 | S
B-33 | Align `StateModel` with what `tf_state` stores, or delete it | It maps `created_at` while the repository writes `createdAt`, which is a trap for B-15 and B-16 | P3 | S

## Tests

The suite does reach the storage layer, including a real `terraform apply` in the scenario test.
Database isolation is done, and the rules it now runs under are described in `AGENTS.md`.
`StateFidelityTest` now compares what comes back out against what went in, across a table of payloads that includes the numeric edge cases and an oversized state, so the two gaps that let H1 and H2 through are closed.
`StateLockRepositoryTest` now races real concurrent inserts rather than simulating the race sequentially, and `StateHistoryTest` asserts that an update writes exactly one `tf_state_history` entry and a create writes none, closing B-38 and B-39.
`ComplexStateScenarioTest` drives a real Terraform lifecycle against a state shape no C# class models, and asserts it is queryable in MongoDB by resource attribute rather than only readable back through the API.
A `WebApi.UnitTests` project now sits alongside the integration suite, covering the H1/H2 numeric conversion in isolation.

ID   | Change | Why | Priority | Size
---- | ------ | --- | -------- | ----
B-50 | Decide whether deleting a state should also delete its history | `StateRepository.DeleteAsync` removes only the `tf_state` document, so history entries survive their state forever in production. The scenario and resource tests now clean up all three collections themselves, which leaves only the product question | P2 | S

## Features

ID   | Change | Why | Priority | Size
---- | ------ | --- | -------- | ----
B-14 | Capture caller run context, see the [feasibility study](feasibility-run-context.md) | Requested, and there is no way to tell which repository or branch produced a state | P2 | M
B-15 | Keep previous versions in a `tf_state_revision` collection (**needs decision**, changes the data model) | Planned for V2, and the current forward-only patch history cannot reconstruct a past state | P2 | M
B-16 | State history API: list revisions, fetch one, reconstruct a past state | The history is written but there is no way to read it | P2 | M/L
B-19 | OpenTelemetry SDK-native instrumentation with custom spans | Auto-instrumentation gives no visibility into MongoDB operations | P2 | M
B-17 | Administrative API for states, tenants and users | Administration is only possible through `tfbeadm` | P3 | M
B-18 | Stale lock handling: lock age, optional TTL, force-unlock runbook | A crashed run leaves a lock that nothing will ever release | P3 | S/M

## Operations and developer experience

ID   | Change | Why | Priority | Size
---- | ------ | --- | -------- | ----
B-24 | Provision MongoDB for tests through Testcontainers or compose | B-47 to B-49 remove the need for this locally, so what is left is CI, where a container also supplies the replica set that B-30 needs | P3 | M
B-21 | Retention or TTL policy for `tf_state_history` | The collection grows without bound | P2 | S
B-25 | Derive the OpenAPI document version from `VersionPrefix` | Two version numbers are kept in sync by hand, and the 1.3.0 release had to touch both | P2 | S
B-54 | Declare the response body of the `409` on the state and lock endpoints | It returns the existing lock, but the generated OpenAPI now infers `ProblemDetails` for it, which is wrong and became visible when `400` and `413` were declared | P3 | S
B-26 | Add a `CHANGELOG.md` or automated release notes | Releases have no record of what changed | P3 | S
