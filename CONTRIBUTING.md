# Contribution guide

## Go through the codebase

The application source code is in the following .NET projects:

Project name               | Technology  | Project type
---------------------------|-------------|---------------------------
`Common.AspNetCore`        | .NET 10     | Library
`Common.AspNetCore.WebApi` | .NET 10     | Library
`Common.MongoDb`           | .NET 10     | Library
`Domain`                   | .NET 10     | Library
`Infrastructure.MongoDb`   | .NET 10     | Library
`WebApi`                   | ASP.NET 10  | Web application (REST API)

The application is using the following .NET packages (via NuGet):

Name                     | Description
-------------------------|-----------------------------
`MongoDB.Bson`           | MongoDB BSON
`MongoDB.Driver`         | MongoDB .NET Driver
`Swashbuckle.AspNetCore` | OpenAPI / Swagger generation
`System.Text.Json`       | JSON support

The code was made by looking at Terraform specifications:

- [HTTP backend](https://developer.hashicorp.com/terraform/language/backend/http)
- [Remote state backend](https://github.com/hashicorp/terraform/tree/main/internal/backend/remote-state)

## Debug the application

A MongoDB must be running - the easiest way to do it is through a container (here with Docker CLI/engine):

```bash
docker run --name mongodb -d -p 27017:27017 mongo:8.2
```

Add the test user:

```bash
MONGODB_CONTAINERNETWORK=bridge MONGODB_CONTAINERNAME=mongodb ./scripts/tfbeadm create-user admin admin123 dummy
```

Run the web API from the build files ([.NET 10](https://dotnet.microsoft.com/download) must be installed):

```bash
dotnet run --project src/WebApi
```

Open Swagger in a browser: [localhost:5293/swagger](http://localhost:5293/swagger).

Or, debug from an IDE, such as Visual Studio Community 2022 or Rider - and open [localhost:5000/swagger](http://localhost:5000/swagger).

Once you're done, stop the container:

```bash
docker stop mongodb
```

## Run the application from the sources in a container

If you just want to run the application, the easiest way is through containers (application + database) - there is a Docker compose file for it:

```bash
docker compose up --build
```

Add the test user:

```bash
docker compose run --rm dbinit
```

Open [localhost:9001/swagger](http://localhost:9001/swagger)

## Use the Swagger website

If you see an error, make sure to refresh the cache of the page, it can happen if the version of the application has changed.

Assuming you successfully reached the Swagger website, you need to authenticate by clicking on **Authorize** and use username=admin, and password=admin123.

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

Name      | Role                     | Definition file
----------|--------------------------|----------------------------
**CI**    | Continuous Integration   | `.github/workflows/ci.yaml`
**PKG**   | Continuous Delivery      | `.github/workflows/pkg.yaml`
**Pages** | Continuous Documentation | `.github/workflows/pages.yaml`

GitHub Variables are defined (in **General** / **Security** / **Secrets and Variables** / **Actions**):

- `DOCKERHUB_TOKEN`
- `DOCKERHUB_USERNAME`
- `SONAR_HOST_URL`
- `SONAR_ORG`
- `SONAR_PROJECT_KEY`
- `SONAR_TOKEN`
