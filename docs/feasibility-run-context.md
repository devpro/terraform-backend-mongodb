# Feasibility study: capturing caller run context

Study date: 2026-07-10.
Related backlog item: B-14 in the [backlog](backlog.md).

## Goal

When Terraform or OpenTofu calls the backend, record *where the run came from*:

- Git repository remote URL, branch, commit SHA, and whether the working tree was dirty or clean.
- Execution environment: local workstation vs pipeline, and if a pipeline, which CI system and which run (URL, run ID).
- Terraform metadata: version, operation (`plan`/`apply`/`destroy`), user/host.

The data lands in the same MongoDB database already consumed by the *liveship* project, which can then correlate infrastructure state changes with the code and pipeline that produced them.

## The core constraint

The [Terraform HTTP backend protocol](https://developer.hashicorp.com/terraform/language/backend/http) is fixed: the CLI sends only the state payload, the lock payload, Basic auth credentials, and the `ID` query parameter.
There is no protocol extension point for custom metadata, so any solution combines what the server can already infer with something the client (or a wrapper around it) volunteers.

### What the backend already receives for free

The lock payload (`StateLockModel`) carries `Who` (user@host), `Version` (Terraform version), `Operation`, `Created`, and `Path`, which Terraform fills in on every `lock` call.
Basic auth identifies the tenant and user, and the connection provides a source IP.
None of this includes git information, but it is enough to attribute *who* changed *what* and *when* without any client cooperation.

## Transport options for client-provided context

### Option A: custom headers (OpenTofu only)

OpenTofu's HTTP backend supports an optional [`headers` map](https://opentofu.org/docs/language/settings/backends/http/) in the backend block, so a pipeline can inject e.g. `X-TF-Context: <base64 JSON>` generated before `tofu init`.
HashiCorp Terraform's HTTP backend has **no custom header support** (a long-standing feature request), so this option alone excludes Terraform users.

- Pros: clean, invisible in URLs and access logs, natural fit for structured data.
- Cons: OpenTofu-only; headers must be baked into the backend block or partial backend config at `init` time.

### Option B: query parameters on the backend address

`TF_HTTP_ADDRESS`, `TF_HTTP_LOCK_ADDRESS`, and `TF_HTTP_UNLOCK_ADDRESS` accept any URL, including a query string, e.g.:

```bash
export TF_HTTP_ADDRESS="https://backend.example.com/acme/state/app?runId=${RUN_ID}&branch=${BRANCH}&dirty=false&env=github"
```

The HTTP backend client copies the configured URL and *adds* the `ID` lock parameter to the existing query, so caller-supplied parameters are preserved on every GET/POST/DELETE.
This behavior is an implementation detail rather than a documented guarantee, so it must be locked in with a scenario test.
The existing `Scenarios/` infrastructure makes this cheap: pass a decorated `TF_HTTP_ADDRESS` and assert the context was stored.

- Pros: works with both Terraform and OpenTofu today, zero protocol changes, trivial server-side parsing.
- Cons: limited payload size, values appear in access logs and proxies (fine for git metadata, never for secrets), relies on tested-but-undocumented client behavior.

### Option C: dedicated context endpoint

Add an authenticated endpoint, e.g. `POST /{tenant}/state/{name}/context`, that a wrapper calls once before running Terraform with a client-generated `runId` and a rich JSON body.
Combined with Option B carrying only `runId=...`, every subsequent state/lock request correlates to the full context document.

- Pros: unlimited structured payload, explicit contract owned by this project, versionable schema.
- Cons: requires the wrapper to make an extra HTTP call; context and state writes are correlated, not transactional.

### Option D: server-side correlation only (no client changes)

On every state POST/DELETE that carries `?ID=<lockId>`, the backend already looks up the lock; it can persist the lock's `Who`, `Version`, `Operation`, and `Path` into the `tf_state_history` entry (and into a run record).
This ships value immediately with zero adoption effort, but can never provide git or CI information.

## Recommended architecture

Combine the options in phases; each phase is independently useful.

Phase | Scope                                                                                                                           | Effort
----- | ------------------------------------------------------------------------------------------------------------------------------- | ------------
0     | Option D: enrich `tf_state_history` and a new `tf_run_context` collection with lock metadata (who/version/operation)            | S
1     | Options B + A: parse an allowlisted set of query parameters and the `X-TF-Context` header; scenario test for query preservation | M
2     | Publish a `tfrun` wrapper script plus CI snippets (GitHub Actions, GitLab, Jenkins, Azure DevOps) in `scripts/` and docs        | S/M
3     | Option C: dedicated context endpoint for rich payloads, if the query-string budget becomes limiting                             | M (optional)

### Client side: gathering the context

A thin wrapper (bash/PowerShell, or a pipeline step) computes the values and decorates the environment before invoking `terraform`:

```bash
remote=$(git config --get remote.origin.url)
branch=$(git rev-parse --abbrev-ref HEAD)
commit=$(git rev-parse HEAD)
dirty=$([ -n "$(git status --porcelain)" ] && echo true || echo false)
```

Environment detection uses well-known CI variables, falling back to `local`:

Signal                    | Environment
------------------------- | ----------------------------------------------------------------------------------------
`GITHUB_ACTIONS=true`     | `github` (run URL from `GITHUB_SERVER_URL/GITHUB_REPOSITORY/actions/runs/GITHUB_RUN_ID`)
`GITLAB_CI=true`          | `gitlab` (`CI_PIPELINE_URL`)
`JENKINS_URL` set         | `jenkins` (`BUILD_URL`)
`TF_BUILD=True`           | `azure-devops`
`CI=true` (anything else) | `ci-unknown`
none of the above         | `local`

### Server side: storage and processing

- A small action filter or middleware on the state/lock endpoints extracts the header (if present) or the allowlisted query parameters (`runId`, `repo`, `branch`, `commit`, `dirty`, `env`, `runUrl`).
- Context writes go through a bounded channel and a hosted background service so they never add latency or failure modes to state operations.
- New collection `tf_run_context` (camelCase fields per the global convention), indexed on `tenant + name + createdAt`, with an optional TTL for retention:

```json
{
  "tenant": "acme",
  "name": "app",
  "runId": "b7e6…",
  "lockId": "3f2a…",
  "createdAt": "2026-07-10T14:03:00Z",
  "git": { "remoteUrl": "https://github.com/acme/infra", "branch": "main", "commit": "9c1d…", "isDirty": false },
  "environment": { "type": "github", "runUrl": "https://github.com/acme/infra/actions/runs/123" },
  "terraform": { "version": "1.13.2", "operation": "apply", "who": "runner@ci-01" },
  "schemaVersion": 1
}
```

### Integration with liveship

Liveship reads the same database, so the contract is the `tf_run_context` collection schema.
Keep it stable by treating `schemaVersion` as mandatory, only adding optional fields within a version, and documenting the schema in this repository (the producer owns the contract).
State history entries should carry the `runId` so liveship can join "what changed" (`tf_state_history`) with "where it came from" (`tf_run_context`).

## Risks and mitigations

Risk                                                         | Mitigation
------------------------------------------------------------ | -----------------------------------------------------------------------------------------------------------------
Query-parameter preservation is undocumented client behavior | Pin it with a `Scenarios/` test running the real CLI; it fails loudly if a future CLI release changes behavior
Context is client-asserted and spoofable within a tenant     | Acceptable: callers are already authenticated per tenant; treat context as informational, not authorization input
Wrapper adoption is optional, so coverage will be partial    | Phase 0 guarantees a baseline (who/version/operation) for every run regardless of adoption
Query strings leak into access logs and proxies              | Only allow non-sensitive metadata; enforce an allowlist and a size cap (e.g. 2 KB) server-side
HashiCorp Terraform never gets header support                | Option B works for both CLIs; headers are a progressive enhancement for OpenTofu

## Verdict

Feasible with low risk and no changes to the Terraform protocol or CLI.
Phase 0 requires only server-side work and delivers attribution immediately.
The full git/CI context requires a one-line convention in pipelines (decorated `TF_HTTP_ADDRESS`) or the `tfrun` wrapper, both of which degrade gracefully when absent.
