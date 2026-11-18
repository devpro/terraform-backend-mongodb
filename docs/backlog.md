# Backlog

What remains to be done, and why.
Priorities: **P1** before the next release, **P2** planned, **P3** nice to have.
Completed items are not listed: the git history records them.
Identifiers are stable references, not an ordering.

## Constraints

- The data model is a published contract: `tf_state` holds the latest version as queryable BSON, `tf_state_history` the change between versions.
  The 16 MB BSON limit is permanent, and the work is to fail cleanly at it.
- Terraform's `http` backend only sends Basic credentials, so the authentication scheme cannot change.
  Mutual TLS is the only second factor the client supports.

## Security

The database holds every workspace secret in the clear, so this comes first.
Platform items land in the Helm chart in `devpro/helm-charts`, with a matching note in [setup](setup.md).

- **B-53** (P1, platform): rate limit by source IP at the ingress, and alert on bursts of authentication failures.
  An in-process limiter cannot hold one counter across replicas.
- **B-42** (P1, platform): add the HSTS note of the chart README to [setup](setup.md#configuration).
  The chart documents HSTS at the ingress from 0.3.4.
- **B-43** (P1, platform): add the database security note of the chart README to [setup](setup.md): encryption at rest, a read-only user for consuming applications, network isolation, backups treated as secrets.
- **B-12** (P3, app): lower the request body limit below the Kestrel default of 30 MB.
  Above 16 MB, a state is read, parsed and diffed in full before the driver refuses it.
- **B-20** (P3, app): structured audit log for every state and lock operation.
- **B-44** (P3, app): enforce a minimum strength on a password created by `tfbeadm`.
- **B-46** (P3, platform): validate a client certificate at the ingress and pass the verified subject to the application.

## Correctness and performance

- **B-30** (P2): run CI against a replica set, as `compose.yaml` does.
  The MongoDB action of the workflow parts takes a `replica-set` input, but `reusable-dotnet-quality.yml` does not pass one through, so CI runs a standalone server.
  Without a replica set CI never runs the transaction that writes a state and its `tf_state_history_queue` entry together, which only a local run covers.
- **B-21** (P2): retention policy for `tf_state_history`, which grows without bound.
  Deleting a state keeps its history, so retention is also what removes the secrets of a deleted state.
  The `failed` entries of `tf_state_history_queue` hold full states and stay until an operator removes them, so retention covers them too.

## Features

- **B-16** (P2): API to list history entries and reconstruct a past state, since the history is written but never read.
- **B-14** (P2): capture the caller's run context (repository, branch), see the [feasibility study](feasibility-run-context.md).
- **B-19** (P2): OpenTelemetry spans on MongoDB operations that leave the command out, since a state write carries every secret of the workspace.
  The chart turns the auto-instrumentation of MongoDB off for that reason.
- **B-18** (P3): stale lock handling: lock age, optional TTL, force-unlock runbook.
  A crashed run leaves a lock nothing releases.
- **B-54** (P3): declare the `409` response body as the existing lock, since the generated OpenAPI infers `ProblemDetails`.

## Tooling

- **B-59** (P3): run the `code-quality` and `image-scan` jobs through IstarCI on a commit changing the application, and record which steps pass.
  The Sonar steps cannot pass locally, since a local run holds no `SONAR_TOKEN`.
