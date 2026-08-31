# AGENTS.md

Guidance for coding agents working in this repository.

## What this is

A REST API (ASP.NET Core, .NET 10) implementing the [Terraform HTTP backend protocol](https://developer.hashicorp.com/terraform/language/backend/http) with MongoDB as the storage layer.
Terraform/OpenTofu clients read, write, lock, and unlock state through `/{tenant}/state/{name}` endpoints.
Shipped as a container image (`devprofr/terraform-backend-mongodb`) and a Helm chart.

## The storage contract

**The Terraform state is stored as is, as a queryable BSON document in `tf_state`.**
That is the whole point of this project, not an implementation detail.
Other applications, `liveship` among them, read `tf_state` directly and query it by resource attribute.

Two collections hold it, and the division between them is part of the contract:

- **`tf_state` holds the latest version only**, one document per `{tenant, name}`, never a history of versions.
- **`tf_state_history` holds the computed change** between one version and the next.

The shape of both is a published contract, and it will not change.
The following are settled and are not to be reopened, proposed as options, or re-argued:

- No opaque storage: no GridFS, no compressed `BsonBinaryData`, no state kept as a raw JSON string.
- No restructuring: the `resources` array is not split across documents, and no field is renamed or nested differently.
- No new fields, no new collections, and no change to how a change is computed or stored.

**The data model changes only when the maintainer specifically asks for it.**
A backlog item is not such a request: anything that would add a field, rename one, or introduce a collection needs explicit confirmation first, however well justified it looks.

The 16 MB BSON document limit is an accepted, permanent consequence of this design.
At the ceiling the work is to fail cleanly, with a `413` naming the limit, never to escape the ceiling.

Improvements belong in the .NET layer.
Whatever changes there, the bytes that land in MongoDB keep the same shape.

## Common commands

```bash
dotnet build                      # build the solution (TerraformBackend.slnx)
dotnet run --project src/WebApi   # run the API (http://localhost:5293, Scalar UI at /scalar)
dotnet test                       # run all tests (requires MongoDB + seeded user, see below)
```

Tests use xunit v3 on the Microsoft Testing Platform (`UseMicrosoftTestingPlatformRunner=true`), so runner options go after `--`:

```bash
# single test method
dotnet test test/WebApi.IntegrationTests -- --filter-method "*StateResource_CreateFindDelete_IsSuccess*"
# only tests that don't write to the database
dotnet test test/WebApi.IntegrationTests -- --filter-trait "Mode=Readonly"
```

### Test prerequisites

All tests are integration tests hitting a real MongoDB on `localhost:27017`.
A running server is the only prerequisite:

```bash
docker run --name mongodb -d -p 27017:27017 mongo:8.2
```

Nothing is seeded by hand.
The suite owns its database, `tfbackend_integrationtests`, and `TestDatabaseFixture` creates the indexes and the `admin`/`dummy` account it authenticates as, then removes the account at the end of the run.
The `Scenarios/` tests additionally run the real `terraform` CLI (must be on PATH) against a Kestrel-hosted instance, applying the samples under `samples/`.

Full stack via containers: `docker compose up` (API on :9001), then `docker compose run --rm dbinit` to seed a user for manual use.

### Test database rules

The suite runs against a real, long-lived MongoDB rather than a throwaway one per test, so three rules hold, and each replaced something that failed in practice.

- **The suite never writes to `tfbackend_dev`.**
  `IntegrationTestDatabase.Name` resolves `DatabaseSettings__DatabaseName`, defaulting to `tfbackend_integrationtests`, and `TestHostConfiguration` pushes it into the host through `UseSetting`.
  Pushing it is the load-bearing half: unset, the in-process host runs as `Development` and falls back to the maintainer's own database silently.
  `TestDatabaseGuard` then refuses any name containing `dev`, `prod`, `staging` or `preprod`.
- **Every test removes what it created, on success and on failure.**
  `DatabaseTestBase.TrackState` and `TrackCleanup` register the undo at the moment of creation, never in a `try`/`finally`, which cannot cover a fixture created before the `try` opened.
  Cleanups run in reverse order under `CancellationToken.None`, since the test's own token is cancelled exactly when a run times out and leftovers are most likely.
  This matters because `tf_state` and `tf_state_lock` are uniquely indexed on `{tenant, name}`, so a leftover breaks the next run.
  Deleting a state through the API does not remove its `tf_state_history` entries, so `TrackState` covers all three collections.
- **The run proves it left the database as it found it.**
  `TestDatabaseFixture` counts every collection before seeding and again at the end, and fails naming the collection that grew.
  A delete filter that matches nothing reports success, so a passing suite is not evidence on its own.
  It reports rather than deletes: removing documents the run did not create would hide the leak.

## Architecture

Three projects with one-way dependencies: `WebApi` > `Infrastructure.MongoDb` > `Domain`.

- **`src/Domain`**: repository interfaces (`IStateRepository`, `IStateLockRepository`, `IUserRepository`, `ILockoutRepository`) and models.
  No logic, no infrastructure references.
- **`src/Infrastructure.MongoDb`**: repository implementations.
  Collections: `tf_state` (current state, stored as a raw `BsonDocument`, since the domain `StateModel` is only used by test fakers), `tf_state_lock`, `user` (BCrypt password hashes), `auth_lockout` (failed-attempt counters, TTL-expired).
  `tf_state_history` holds JSON-diff patches computed with `SystemTextJson.JsonDiffPatch` on every state update.
- **`src/WebApi`**: `StateController` (all protocol logic including lock checking), Basic authentication against the `user` collection, `TenantAuthorizationFilter` matching the route `{tenant}` against the user's tenant claim, and `ThrottledCredentialAuthenticator` enforcing the failed-attempt lockout through `auth_lockout`.
  DI wiring lives in `WebApi/DependencyInjection/`.

### Protocol constraints

`StateController` implements the Terraform HTTP backend contract.
Do not change the following without checking the spec:

- Lock ID comes as query param `ID` (uppercase) on state POST/DELETE, and as JSON body (`StateLockModel`, `ID` property) on lock endpoints.
- Locked state means HTTP 423; a lock held by someone else means HTTP 409 with the existing lock as body.
- GET state returns the raw state JSON as `text/plain`.
- POST state is an upsert (create *and* update).

### Configuration

`ApplicationConfiguration` wraps `IConfiguration`: `DatabaseSettings:ConnectionString`, `DatabaseSettings:DatabaseName`, `Features:IsScalarEnabled`, `Features:IsHttpsRedirectionEnabled`.
Override via env vars with `__` separators, for example `DatabaseSettings__ConnectionString`.

## Writing style

These rules apply to Markdown, code comments, commit messages, and any prose in scripts.

**One sentence per line.**
A line break only ever happens at the end of a sentence, and a sentence is never wrapped across two lines.
There is no maximum line length: screens are wide, and the 80 character convention is not used here.
Wrapping is handled by the editor, not by hard newlines.

**Never use the em dash (`—`) or the en dash (`–`).**
Use a colon when introducing an explanation, a comma when joining clauses, or a full stop and a new sentence.
This applies to prose, code comments, table cells, and error message strings.

**Never use the second person.**
No "you", no "your", not even in placeholders such as `<your-token>`, which should read `<token>`.
The documentation describes the repository, it does not address a reader.
Write "the working tree", not "your working tree".

**Other conventions.**
Use `ini` as the fence language for `.properties` blocks, never `properties`.
Prefer `>` over `→` when describing UI navigation, for example **Project Settings > Quality Gate**.

## Conventions

### Build and code

- NuGet versions are managed centrally in `Directory.Packages.props`: `PackageReference` entries in csproj files have no `Version` attribute.
- Release version is `VersionPrefix` in `Directory.Build.props`.
- MongoDB field names are camelCase via a global `ConventionPack` (registered in `InfrastructureServiceCollectionExtensions`).
- Markdown and YAML are linted in CI (`.markdownlint-cli2.yaml`, `.yamllint.yaml`); C# style is enforced by `.editorconfig`.
- Do not run markdownlint: linting is run manually by the maintainer and in CI.

### Scripts

Shell scripts are named in `snake_case`, which is the standard for bash: `sonar_bootstrap.sh`, not `sonar-bootstrap.sh`.

Scripts must be committed with the executable bit set.
A script committed as `100644` fails on a fresh clone even though it works locally:

```bash
git update-index --chmod=+x path/to/script.sh
```

### Documentation

Docs site is MkDocs Material (`docs/`, `mkdocs.yml`), deployed by the Pages workflow.
The root `README.md` stays as short as possible.
Shared content lives in `docs/` and is linked, never copied.
Contributor-facing material lives in `CONTRIBUTING.md` at the repository root.
Each sample README is self-sufficient for that sample and links out for anything generic.

The current assessment of the code is in [docs/code-review.md](docs/code-review.md), and what remains to be done is in [docs/backlog.md](docs/backlog.md).
Both are kept up to date rather than re-derived.

### Target platform

Target platform is Linux with Docker, including WSL2, and `bash`.
