# Contribution guide

## Application codebase

### .NET projects

Project name               | Technology | Project type
---------------------------|------------|---------------------------
`Common.AspNetCore`        | .NET 8     | Library
`Common.AspNetCore.WebApi` | .NET 8     | Library
`Common.MongoDb`           | .NET 8     | Library
`Domain`                   | .NET 8     | Library
`Infrastructure.MongoDb`   | .NET 8     | Library
`WebApi`                   | ASP.NET 8  | Web application (REST API)

### .NET packages (NuGet)

Name                     | Description
-------------------------|-----------------------------
`MongoDB.Bson`           | MongoDB BSON
`MongoDB.Driver`         | MongoDB .NET Driver
`Swashbuckle.AspNetCore` | OpenAPI / Swagger generation
`System.Text.Json`       | JSON support

### Terraform specifications

- [HTTP backend](https://developer.hashicorp.com/terraform/language/backend/http)
- [Remote state backend](https://github.com/hashicorp/terraform/tree/main/internal/backend/remote-state)

### Other community implementations

- [GitLab](https://gitlab.com/gitlab-org/manage/import/gitlab/-/blob/master/doc/user/infrastructure/terraform_state.md)
  - [lib/api/terraform/state.rb](https://gitlab.com/gitlab-org/manage/import/gitlab/-/blob/master/lib/api/terraform/state.rb)
- HTTP
  - [akshay/terraform-http-backend-pass](https://git.coop/akshay/terraform-http-backend-pass)
  - [bhoriuchi/terraform-backend-http](https://github.com/bhoriuchi/terraform-backend-http)
  - [nimbolus/terraform-backend](https://github.com/nimbolus/terraform-backend)
- git
  - [plumber-cd/terraform-backend-git](https://github.com/plumber-cd/terraform-backend-git)

## Procedures

### Run locally the application

Run MongoDB in a database and add the indexes and test tenant/user:

```bash
docker run --name mongodb -d -p 27017:27017 mongo:8.0
MONGODB_CONTAINERNAME=mongodb
./scripts/tfbeadm create-indexes
./scripts/tfbeadm create-user admin admin123 dummy
```

Run the web API (example with the command line but an IDE like Visual Studio or Rider would be nice to be able to debug):

```bash
dotnet run --project src/WebApi
```

Open Swagger in a browser: [localhost:5293/swagger](http://localhost:5293/swagger).

## Documentation codebase

The documentation is a static website built with [Material for MkDocs](https://squidfunk.github.io/mkdocs-material/).

Run locally with:

```bash
docker run --rm -it -p 8000:8000 -v ${PWD}:/docs squidfunk/mkdocs-material
```
