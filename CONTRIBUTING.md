# Project development guide

## Application

The code is entirely based on open-source, cross-platform (Linux/Windows), highly performant, free, object-oriented technologies: .NET / C#.

### Projects

Project name               | Technology | Project type
---------------------------|------------|---------------------------
`Common.AspNetCore`        | .NET 8     | Library
`Common.AspNetCore.WebApi` | .NET 8     | Library
`Common.MongoDb`           | .NET 8     | Library
`Domain`                   | .NET 8     | Library
`Infrastructure.MongoDb`   | .NET 8     | Library
`WebApi`                   | ASP.NET 8  | Web application (REST API)

### Packages (NuGet)

Name                     | Description
-------------------------|-----------------------------
`MongoDB.Bson`           | MongoDB BSON
`MongoDB.Driver`         | MongoDB .NET Driver
`Swashbuckle.AspNetCore` | OpenAPI / Swagger generation
`System.Text.Json`       | JSON support

### Clients

- [OpenTofu](https://opentofu.org/)
- [Terraform](https://www.terraform.io)
  - [HTTP backend](https://developer.hashicorp.com/terraform/language/backend/http)
  - [Remote state backend](https://github.com/hashicorp/terraform/tree/main/internal/backend/remote-state)

### Backing services

- [MongoDB](https://www.mongodb.com/)

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

Create/have a MongoDB database (example with a local container but you can provision a cluster in MongoDB Atlas):

```bash
# creates a container
docker run --name mongodb -d -p 27017:27017 mongo:8.0
# (optional) adds indexes for optimal performances
docker run --rm --link mongodb \
  -v "$(pwd)/scripts":/home/scripts mongo:8.0 \
  bash -c "mongosh mongodb://mongodb:27017/terraform_backend_dev /home/scripts/mongo-create-index.js"
# creates one user
./scripts/mongo-create-user.sh admin admin123 dummy
docker run --rm --link mongodb \
  -v "$(pwd)/scripts":/home/scripts mongo:8.0 \
  bash -c "mongosh mongodb://mongodb:27017/terraform_backend_dev /home/scripts/add-user.js"
```

Run the web API (example with the command line but an IDE like Visual Studio or Rider would be nice to be able to debug):

```bash
dotnet run --project src/WebApi
```

Open Swagger in a browser: [localhost:5293/swagger](http://localhost:5293/swagger).

## Documentation

The documentation is a static website built with [Material for MkDocs](https://squidfunk.github.io/mkdocs-material/).

Run locally with:

```bash
docker run --rm -it -p 8000:8000 -v ${PWD}:/docs squidfunk/mkdocs-material
```
