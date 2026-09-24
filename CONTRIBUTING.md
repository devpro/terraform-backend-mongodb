# Contribution guide

## Codebase

Project name               | Technology  | Project type
---------------------------|-------------|---------------------------
`Domain`                   | .NET 10     | Library (models and repository interfaces)
`Infrastructure.MongoDb`   | .NET 10     | Library (MongoDB repository implementations)
`WebApi`                   | ASP.NET 10  | Web application (REST API)
`WebApi.UnitTests`         | .NET 10     | Test project (xunit v3)
`WebApi.IntegrationTests`  | .NET 10     | Test project (xunit v3, real MongoDB and Terraform CLI)

Main NuGet packages, with versions managed centrally in `Directory.Packages.props`:

Name                           | Description
-------------------------------|-------------------------------------
`BCrypt.Net-Next`              | Password hashing
`MongoDB.Driver`               | MongoDB .NET Driver (includes BSON)
`Scalar.AspNetCore`            | OpenAPI web UI
`SystemTextJson.JsonDiffPatch` | JSON diffs for state history
`Withywoods.Configuration`     | Configuration helpers

The architecture is described in [docs/architecture.md](docs/architecture.md), and follows the Terraform [HTTP backend](https://developer.hashicorp.com/terraform/language/backend/http) and [remote state backend](https://github.com/hashicorp/terraform/tree/main/internal/backend/remote-state) specifications.

## Debug the application

Start MongoDB in a container:

```bash
docker run --name mongodb -d -p 27017:27017 mongo:8.2
```

Create the indexes and a user, whose password is prompted for:

```bash
MONGODB_CONTAINERNETWORK=bridge MONGODB_CONTAINERNAME=mongodb ./scripts/tfbeadm create-indexes
MONGODB_CONTAINERNETWORK=bridge MONGODB_CONTAINERNAME=mongodb ./scripts/tfbeadm create-user admin dummy
```

Run the web API with the [.NET 10 SDK](https://dotnet.microsoft.com/download), or debug it from an IDE:

```bash
dotnet run --project src/WebApi
```

Scalar is served on [localhost:5293/scalar](http://localhost:5293/scalar).
**Authorize** takes the user created above.
A stale page after an upgrade is fixed by clearing the browser cache.

Stop the database when done:

```bash
docker stop mongodb
```

## Run from the sources in containers

`compose.yaml` runs the application and the database, and `dbinit` seeds a test user:

```bash
docker compose up
docker compose run --rm dbinit
```

Scalar is then served on [localhost:9001/scalar](http://localhost:9001/scalar).

Remove the containers:

```bash
docker compose rm --force
```

Build the container image alone:

```bash
docker build . -t terraform-backend-mongodb:local -f src/WebApi/Dockerfile
```

## Run the tests

The tests need MongoDB on `localhost:27017` and `terraform` on the `PATH`, and set up and clean their own database:

```bash
dotnet test
```

## Preview the documentation website

The documentation is a static website built with [Material for MkDocs](https://squidfunk.github.io/mkdocs-material/), served with live reload on [localhost:8000](http://localhost:8000/):

```bash
docker run --rm -it -p 8000:8000 -v "${PWD}:/docs" squidfunk/mkdocs-material serve --dev-addr=0.0.0.0:8000 --livereload --dirtyreload --watch docs --watch mkdocs.yml
```

To update Material for MkDocs, edit `docs/requirements.txt` and regenerate the lock file:

```bash
python3 -m venv .venv
.venv/bin/pip install pip-tools
.venv/bin/pip-compile docs/requirements.txt --generate-hashes --output-file docs/requirements.lock
```

## Automation

Name  | Role                     | Definition file
------|--------------------------|-------------------------------
CI    | Continuous Integration   | `.github/workflows/ci.yaml`
PKG   | Continuous Delivery      | `.github/workflows/pkg.yaml`
Pages | Continuous Documentation | `.github/workflows/pages.yaml`

The workflows read these GitHub secrets and variables, set in **Settings > Secrets and variables > Actions**:

- `DOCKERHUB_TOKEN`
- `DOCKERHUB_USERNAME`
- `SONAR_HOST_URL`
- `SONAR_ORG`
- `SONAR_PROJECT_KEY`
- `SONAR_TOKEN`

The `Jenkinsfile` exists for demo purposes.
When Jenkins authenticates with a personal access token rather than a GitHub App, it needs a webhook, in **Settings > Webhooks > Add webhook**:

- Payload URL: `https://<JENKINS_DOMAIN>/github-webhook/`
- Content type: `application/json`
- Events: push and pull requests
