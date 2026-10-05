# Contribution guide

## Codebase

Project name              | Technology | Project type
--------------------------|------------|--------------------------------------------------------
`Domain`                  | .NET 10    | Library (models and repository interfaces)
`Infrastructure.MongoDb`  | .NET 10    | Library (MongoDB repository implementations)
`WebApi`                  | ASP.NET 10 | Web application (REST API)
`WebApi.UnitTests`        | .NET 10    | Test project (xunit v3)
`WebApi.IntegrationTests` | .NET 10    | Test project (xunit v3, real MongoDB and Terraform CLI)

Main NuGet packages, with versions managed centrally in `Directory.Packages.props`:

Name                           | Description
-------------------------------|------------------------------------
`BCrypt.Net-Next`              | Password hashing
`MongoDB.Driver`               | MongoDB .NET Driver (includes BSON)
`Scalar.AspNetCore`            | OpenAPI web UI
`SystemTextJson.JsonDiffPatch` | JSON diffs for state history
`Withywoods.Configuration`     | Configuration helpers

The architecture is described in [docs/architecture.md](docs/architecture.md),
and follows the Terraform [HTTP backend](https://developer.hashicorp.com/terraform/language/backend/http)
and [remote state backend](https://github.com/hashicorp/terraform/tree/main/internal/backend/remote-state) specifications.

## Debug the application

Start MongoDB in a container:

```bash
docker run --name mongodb -d -p 27017:27017 mongo:8.2
```

Create the indexes and a user `admin` in the tenant `dummy`, whose password is prompted for:

```bash
export MONGODB_URI=mongodb://localhost:27017/tfbackend_dev
./scripts/tfbeadm create-indexes
./scripts/tfbeadm create-user admin dummy
```

`tfbeadm` needs `htpasswd` and `mongosh`.
Without `mongosh`, it runs one in a container, reached with `MONGODB_CONTAINERNAME=mongodb MONGODB_CONTAINERNETWORK=bridge` and the default `MONGODB_URI`.

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

Remove the containers and the data:

```bash
docker compose down --volumes
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

The documentation is a static website built with [Material for MkDocs](https://squidfunk.github.io/mkdocs-material/),
served with live reload on [localhost:8000](http://localhost:8000/):

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

[IstarCI](https://github.com/devpro/istarci) is recommended but optional:
the CI is the GitHub Actions pipeline, and IstarCI runs it locally on every commit, in containers, and blocks `git push` when it failed.
It is installed once per machine from GitHub Packages, with a `~/.npmrc` token that reads `@devpro` packages:

```bash
npm install --global @devpro/istarci
istarci daemon install
istarci daemon start
task ci:setup   # registers this repository and installs the pre-push hook
```

Every commit made afterwards runs in the background:

```bash
task ci        # the runs of the recent commits
task ci:logs   # the output of the last run
```

The image of each job, and the workflows left out because they publish or deploy, are set in `.istarci.yml`.
When working on IstarCI itself, `task ci:from-clone` runs the pipeline once from a clone in `~/repos/istarci` or `ISTARCI_DIR`, without the package.
A workflow IstarCI reads wrongly is reported in [devpro/istarci](https://github.com/devpro/istarci), with the workflow file and the job concerned.

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
