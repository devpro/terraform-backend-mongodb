# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A REST API (ASP.NET Core, .NET 10) implementing the [Terraform HTTP backend protocol](https://developer.hashicorp.com/terraform/language/backend/http) with MongoDB as the storage layer.
Terraform/OpenTofu clients read, write, lock, and unlock state through `/{tenant}/state/{name}` endpoints.
Shipped as a container image (`devprofr/terraform-backend-mongodb`) and a Helm chart.

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

All tests are integration tests hitting a real MongoDB on `localhost:27017` (database `tfbackend_dev`, from `src/WebApi/appsettings.Development.json`):

```bash
docker run --name mongodb -d -p 27017:27017 mongo:8.2
MONGODB_URI="mongodb://localhost:27017/tfbackend_dev" ./scripts/tfbeadm create-indexes
MONGODB_URI="mongodb://localhost:27017/tfbackend_dev" ./scripts/tfbeadm create-user admin admin123 dummy
```

Tests authenticate as `admin:admin123` on tenant `dummy` — these exact values are hardcoded in `IntegrationTestBase`.
The `Scenarios/` tests additionally run the real `terraform` CLI (must be on PATH) against a Kestrel-hosted instance, applying the samples under `samples/`.

Full stack via containers: `docker compose up` (API on :9001), then `docker compose run --rm dbinit` to seed the test user.

## Architecture

Three projects with one-way dependencies — `WebApi` → `Infrastructure.MongoDb` → `Domain`:

- **`src/Domain`** — repository interfaces (`IStateRepository`, `IStateLockRepository`, `IUserRepository`) and models. No logic, no infrastructure references.
- **`src/Infrastructure.MongoDb`** — repository implementations.
  Collections: `tf_state` (current state, stored as raw `BsonDocument` — the domain `StateModel` is only used by test fakers), `tf_state_lock`, `user` (BCrypt password hashes).
  `tf_state_history` holds JSON-diff patches computed with `SystemTextJson.JsonDiffPatch` on every state update.
- **`src/WebApi`** — `StateController` (all protocol logic including lock checking), Basic authentication against the `user` collection, and `TenantAuthorizationFilter` matching the route `{tenant}` against the user's tenant claim.
  DI wiring lives in `WebApi/DependencyInjection/`.

### Protocol constraints

`StateController` implements the Terraform HTTP backend contract — don't change these without checking the spec:

- Lock ID comes as query param `ID` (uppercase) on state POST/DELETE, and as JSON body (`StateLockModel`, `ID` property) on lock endpoints.
- Locked state → HTTP 423; lock held by someone else → HTTP 409 with the existing lock as body.
- GET state returns the raw state JSON as `text/plain`; `RawRequestBodyFormatter` handles Terraform's non-JSON content types on input.
- POST state is an upsert (create *and* update).

### Configuration

`ApplicationConfiguration` wraps `IConfiguration`: `DatabaseSettings:ConnectionString`, `DatabaseSettings:DatabaseName`, `Features:IsScalarEnabled`, `Features:IsHttpsRedirectionEnabled`. Override via env vars with `__` separators (e.g. `DatabaseSettings__ConnectionString`).

## Conventions

- NuGet versions are managed centrally in `Directory.Packages.props` — `PackageReference` entries in csproj files have no `Version` attribute.
- Release version is `VersionPrefix` in `Directory.Build.props`.
- MongoDB field names are camelCase via a global `ConventionPack` (registered in `InfrastructureServiceCollectionExtensions`).
- Markdown and YAML are linted in CI (`.markdownlint-cli2.yaml`, `.yamllint.yaml`); C# style is enforced by `.editorconfig`.
- Docs site is MkDocs Material (`docs/`, `mkdocs.yml`), deployed by the Pages workflow.
