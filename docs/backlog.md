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
- **B-42** (P1, platform): add HSTS at the ingress.
  TLS termination is documented as mandatory in [setup](setup.md#configuration), since the password travels on every request.
- **B-43** (P1, platform): document the database as a secret store: encryption at rest, a read-only user for consuming applications, network isolation, backups treated as secrets.
- **B-61** (P1, app): bound the cost of the history diff, which grows with the square of the length of an array that changes entirely.
  A 112 KB request takes 2.8 GB of memory, so one credential holder can have the pod killed for every tenant.
  The stored change is part of the contract, so the bound limits how many diffs run at once, and what happens above a threshold needs a decision.
- **B-62** (P2, app): check the tenant in an authorization filter, ahead of model binding.
  A user of another tenant currently has the body read and parsed before the `401`.
- **B-67** (P2, platform): keep MongoDB tracing off in the Helm chart, as `compose.yaml` does.
  The image enables OpenTelemetry auto-instrumentation, and whether its MongoDB spans carry the command text has not been verified.
- **B-63** (P3, app): count a failed attempt before verifying it, so that parallel guesses cannot overshoot `MaxFailedAttempts`.
- **B-64** (P3, app): answer `400` to a state key holding a NUL character and `401` to an account with a malformed stored hash, both of which answer `500`.
- **B-65** (P3, app): send `Cache-Control: no-store` and `X-Content-Type-Options: nosniff` on a state read, and drop the `Server` header.
- **B-66** (P3, app): join the username and the password in the credential cache key with a separator neither can hold.
- **B-12** (P3, app): lower the request body limit below the Kestrel default of 30 MB.
  Above 16 MB, a state is read, parsed and diffed in full before the driver refuses it.
- **B-20** (P3, app): structured audit log for every state and lock operation.
- **B-44** (P3, app): enforce a minimum strength on a password created by `tfbeadm`.
- **B-46** (P3, platform): validate a client certificate at the ingress and pass the verified subject to the application.
- **B-60** (P1, platform): align the Helm chart's default `webapi.tag` with the tags PKG publishes.
  Chart 0.3.0 defaults to `1.3.0`, while Docker Hub holds `latest` and `1.3.<run id>`, so an install without `--set webapi.tag` cannot pull the image.

## Correctness and performance

- **B-30** (P2): run CI against a replica set, as `compose.yaml` does, through a `mongo:9.0` service declaring `ports: [27017:27017]`.
  It replaces the `sudo apt` and `systemctl start mongod` steps in the `custom-commands` of the quality job, which fail in a container without `systemd`, so the job runs the same on GitHub and in IstarCI.
  Without a replica set CI cannot run transactions, which blocks B-31.
- **B-31** (P2): make the state write and the history write atomic.
  A failure between them leaves a history entry for a transition that never happened.
- **B-29** (P2): stop reading and re-parsing the whole state on every POST.
- **B-50** (P2): decide whether deleting a state also deletes its history.
  `StateRepository.DeleteAsync` removes only the `tf_state` document, so the secrets of a deleted state stay in `tf_state_history`.
- **B-21** (P2): retention policy for `tf_state_history`, which grows without bound.

## Features

- **B-16** (P2): API to list history entries and reconstruct a past state, since the history is written but never read.
- **B-14** (P2): capture the caller's run context (repository, branch), see the [feasibility study](feasibility-run-context.md).
- **B-19** (P2): OpenTelemetry instrumentation with spans on MongoDB operations.
- **B-18** (P3): stale lock handling: lock age, optional TTL, force-unlock runbook.
  A crashed run leaves a lock nothing releases.
- **B-54** (P3): declare the `409` response body as the existing lock, since the generated OpenAPI infers `ProblemDetails`.

## Tooling

- **B-59** (P3): run the `code-quality` and `image-scan` jobs through IstarCI on a commit changing the application, and record which steps pass.
  The Sonar steps cannot pass locally, since a local run holds no `SONAR_TOKEN`.

## Waiting on the maintainer

These change the data model, so they wait for an explicit decision.

- **B-07**: `created_at` in `tf_state` is rewritten on every update, and fixing it needs an `updated_at` field.
- **B-28**: `tf_state_history.upgrade` holds the patch as a string, which reaches the document limit before the state does.
