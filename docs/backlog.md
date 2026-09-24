# Backlog

What needs to change, and why.
Sourced from the [code review](code-review.md), the V2 ideas in [project](project.md), and new feature requests.
Priorities: **P1** = before the next release, **P2** = planned, **P3** = nice to have.
Size: S (hours), M (days), L (weeks).

Completed items are not listed: the review's [fixed findings](code-review.md#fixed-findings) and the git history record them.
Identifiers are stable references, not an ordering.

## Fixed constraints

Two constraints rule out whole classes of solution.

The data model is a published contract: other applications query it by resource attribute, and it will not change.
`tf_state` holds the latest version only, one document per `{tenant, name}`, and `tf_state_history` holds the computed change between one version and the next.
That rules out making the state opaque (GridFS, compressed binary, a raw JSON string) and rules out restructuring it (splitting `resources` into separate documents).
The 16 MB BSON document limit is therefore permanent, and the work is to fail cleanly at it rather than to escape it.

Improvements belong in the .NET layer, not in the stored representation.
The model changes only when the maintainer specifically asks for it, and an item in this backlog is not such a request.

Three items below would change it, so they are marked **needs decision** and wait on the maintainer:

- **B-07** adds an `updated_at` field to `tf_state`.
- **B-28** changes the `tf_state_history` document: the patch stored as BSON rather than as a string, and the `upgrade` field renamed.
- **B-15** introduces a `tf_state_revision` collection, which conflicts with `tf_state` holding the latest version only.

The second constraint is the client.
Terraform's `http` backend sends a Basic credential on every request and supports no bearer token, no OAuth flow and no custom headers, so the authentication scheme cannot be replaced.
It does support mutual TLS (`client_certificate_pem`, `client_private_key_pem`, `client_ca_certificate_pem`), which is the only Terraform-native way to add a second factor.

## Security

Ranked first because the application is in daily production use and its database holds, in the clear, every secret of every managed workspace.
The application's half is done, and what remains is mostly the platform's, as [set out in the review](code-review.md#where-each-control-belongs).
Platform items land in the Helm chart in `devpro/helm-charts`, so each needs an issue there and a matching note in [setup](setup.md).

ID   | Change | Why | Where | Priority | Size
---- | ------ | --- | ----- | -------- | ----
B-53 | Rate limit by source IP at the ingress, and alert on authentication-failure bursts through the existing collector | The edge rejects a request before any CPU is spent on it and holds one counter across every replica, which an in-process limiter cannot | Platform | P1 | S
B-42 | Document that TLS termination is mandatory, and add HSTS | The password is replayed on every request, so one plaintext hop exposes every workspace in the tenant, and `skip_cert_verification` silently defeats the protection it appears to configure | Platform | P1 | S
B-43 | Document the database as a secret-bearing store: encryption at rest, a least-privilege read-only user for consuming applications, network isolation, and backups treated as secrets | A Terraform state holds provider credentials and private keys in plaintext, and the storage contract makes `tf_state` directly readable by other applications | Platform | P1 | S
B-44 | Enforce a minimum strength on a credential created by `tfbeadm` | The prompt and the generated-credential guidance are in place, but nothing stops a weak password being accepted, and no rate limit at the edge makes one safe | App | P3 | S
B-13 | Document secret management options for the deployment itself | Deployments have no guidance on mounted secret files or a secrets manager for the MongoDB connection string | Platform | P3 | S
B-46 | Validate a client certificate at the ingress and pass the verified subject to the application | It is the only second factor the Terraform client can offer, and the ingress already owns certificate distribution and revocation | Platform | P3 | M
B-12 | Lower the request body size limit below the Kestrel default of 30 MB | The default sits above the 16 MB BSON ceiling, so an oversized state is read, parsed and diffed in full before the driver refuses it | App | P3 | S
B-20 | Structured audit log for every state and lock operation | There is no record of who changed what | App | P3 | S

## Correctness

ID   | Change | Why | Priority | Size
---- | ------ | --- | -------- | ----
B-28 | Store history patches as BSON documents and rename the `upgrade` field (**needs decision**, changes the data model) | The string-encoded patch reaches the document limit before the state does, and the field name does not say what it holds | P2 | S
B-29 | Stop reading and re-parsing the whole state on every POST | Every apply pays a full read, parse and diff, whether or not the history is ever read | P2 | M
B-30 | Run CI against a replica set, as `compose.yaml` already does | CI cannot run transactions today, which blocks B-31 and B-15 | P2 | S
B-31 | Make the state write and the history write atomic | A failure between them leaves a history entry describing a transition that never happened | P2 | S
B-07 | Keep `created_at` on update and add `updated_at` (**needs decision**, changes the data model) | `created_at` currently records the last update, so the creation time is lost | P3 | S

## Tests

The suite's coverage and remaining gaps are in the [review](code-review.md#test-coverage).

ID   | Change | Why | Priority | Size
---- | ------ | --- | -------- | ----
B-50 | Decide whether deleting a state should also delete its history | `StateRepository.DeleteAsync` removes only the `tf_state` document, so history entries outlive their state | P2 | S

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
B-24 | Provision MongoDB for CI through a `services:` container or compose | It replaces the inline install, and supplies the replica set B-30 needs | P3 | M
B-58 | Decide on `Verify.XunitV3` 33 | From 33.0 the build fails until a sponsorship or licence-exemption property is declared, so it stays on 32.x | P3 | S
B-21 | Retention or TTL policy for `tf_state_history` | The collection grows without bound | P2 | S
B-54 | Declare the response body of the `409` on the state and lock endpoints | It returns the existing lock, but the generated OpenAPI infers `ProblemDetails` for it | P3 | S
B-26 | Add a `CHANGELOG.md` or automated release notes | Releases have no record of what changed | P3 | S
