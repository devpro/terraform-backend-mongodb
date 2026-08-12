# Contribution guide

## Go through the codebase

The application source code is in the following .NET projects:

Project name               | Technology  | Project type
---------------------------|-------------|---------------------------
`Domain`                   | .NET 10     | Library (models & repository interfaces)
`Infrastructure.MongoDb`   | .NET 10     | Library (MongoDB repository implementations)
`WebApi`                   | ASP.NET 10  | Web application (REST API)
`WebApi.IntegrationTests`  | .NET 10     | Test project (xunit v3)

The application is using the following main .NET packages (via NuGet, versions centrally managed in `Directory.Packages.props`):

Name                           | Description
-------------------------------|-------------------------------------
`BCrypt.Net-Next`              | Password hashing
`MongoDB.Driver`               | MongoDB .NET Driver (includes BSON)
`Scalar.AspNetCore`            | OpenAPI web UI
`SystemTextJson.JsonDiffPatch` | JSON diffs for state history
`Withywoods.Configuration`     | Configuration helpers

The architecture is described in [docs/architecture.md](docs/architecture.md).

The code was made by looking at Terraform specifications:

- [HTTP backend](https://developer.hashicorp.com/terraform/language/backend/http)
- [Remote state backend](https://github.com/hashicorp/terraform/tree/main/internal/backend/remote-state)

## Debug the application

A MongoDB must be running - the easiest way to do it is through a container (here with Docker CLI/engine):

```bash
docker run --name mongodb -d -p 27017:27017 mongo:8.2
```

Configure the database (replace xxx by the password you want):

```bash
MONGODB_CONTAINERNETWORK=bridge MONGODB_CONTAINERNAME=mongodb ./scripts/tfbeadm create-indexes
MONGODB_CONTAINERNETWORK=bridge MONGODB_CONTAINERNAME=mongodb ./scripts/tfbeadm create-user admin dummy
```

Run the web API from the build files ([.NET 10](https://dotnet.microsoft.com/download) must be installed):

```bash
dotnet run --project src/WebApi
```

Open Scalar in a browser: [localhost:5293/scalar](http://localhost:5293/scalar).

Or, debug from an IDE, such as Visual Studio Community 2022 or Rider - and open [localhost:5000/scalar](http://localhost:5000/scalar).

Once you're done, stop the container:

```bash
docker stop mongodb
```

## Run the application from the sources in a container

If you just want to run the application, the easiest way is through containers (application + database) - there is a Docker compose file for it:

```bash
docker compose up
```

<!--
docker compose build --no-cache
-->

Add the test user:

```bash
docker compose run --rm dbinit
```

Open [localhost:9001/scalar](http://localhost:9001/scalar)

Delete the containers:

```bash
docker compose rm --force
```

You can also build the container image:

```bash
docker build . -t terraform-backend-mongodb:local -f src/WebApi/Dockerfile
```

<!-- not fully working
And run the container with:

```bash
docker run -it --rm --name todoblazorlocal \
  --link "mongodb" --network "bridge" \
  -p 9001:8080 -e ASPNETCORE_ENVIRONMENT=Development \
  terraform-backend-mongodb:local
```
-->

## Use the Scalar website

If you see an error, make sure to refresh the cache of the page, it can happen if the version of the application has changed.

Assuming you successfully reached the Scalar website, you need to authenticate by clicking on **Authorize** and use username=admin, and password=xxx.

Then, you can try the different commands.

## Run the tests

Test projects are run in the CI pipeline to ensure no regression are introduced with new versions - you can (should) run with:

```bash
dotnet test
```

## Preview the documentation website

The documentation is a static website built with [Material for MkDocs](https://squidfunk.github.io/mkdocs-material/).

Run locally with:

```bash
docker run --rm -it -p 8000:8000 -v ${PWD}:/docs squidfunk/mkdocs-material
```

Open [localhost:8000](http://localhost:8000/).

You can also use hot reload to view changes without having to restart the container:

```bash
docker run --rm -it -p 8000:8000 -v "${PWD}:/docs" squidfunk/mkdocs-material serve --dev-addr=0.0.0.0:8000 --livereload --dirtyreload --watch docs --watch mkdocs.yml
```

## Understand the application lifecycle automation

GitHub Actions are triggered to automate the integration and delivery of the application:

Name  | Role                     | Definition file
------|--------------------------|-------------------------------
CI    | Continuous Integration   | `.github/workflows/ci.yaml`
PKG   | Continuous Delivery      | `.github/workflows/pkg.yaml`
Pages | Continuous Documentation | `.github/workflows/pages.yaml`

GitHub Variables are defined (in **General** / **Security** / **Secrets and Variables** / **Actions**):

- `DOCKERHUB_TOKEN`
- `DOCKERHUB_USERNAME`
- `SONAR_HOST_URL`
- `SONAR_ORG`
- `SONAR_PROJECT_KEY`
- `SONAR_TOKEN`

Jenkinsfile has also been added for demo purposes.

In that case go to Settings → Webhooks → Add webhook and set:

- Payload URL: `https://<JENKINS_DOMAIN>/github-webhook/`
- Content type: application/json
- Events: Push event + Pull requests

Note: if a GitHub App is configured for Jenkins with webhook configuration, the webhook doesn't need to be configured separately.
If using a PAT (not a GitHub App), webhook is needed.

## Update Material for MkDocs

Update `docs/requirements.txt` and run in bash terminal:

```bash
python3 -m venv .venv
.venv/bin/pip install pip-tools
.venv/bin/pip-compile docs/requirements.txt --generate-hashes --output-file docs/requirements.lock
```
