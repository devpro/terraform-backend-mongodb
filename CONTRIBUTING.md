# Project development guide

## Design

The application is entirely based on open-source, cross-platform (Linux/Windows), highly performant, free, object-oriented technologies: .NET / C#.

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

### Documentation

* [OpenTofu](https://opentofu.org/)
* [MongoDB](https://www.mongodb.com/)
* [Terraform](https://www.terraform.io)
  * [HTTP backend](https://developer.hashicorp.com/terraform/language/backend/http)
  * [Remote state backend](https://github.com/hashicorp/terraform/tree/main/internal/backend/remote-state).

### References of other implementations

* [GitLab](https://gitlab.com/gitlab-org/manage/import/gitlab/-/blob/master/doc/user/infrastructure/terraform_state.md)
  * [lib/api/terraform/state.rb](https://gitlab.com/gitlab-org/manage/import/gitlab/-/blob/master/lib/api/terraform/state.rb)
* HTTP
  * [akshay/terraform-http-backend-pass](https://git.coop/akshay/terraform-http-backend-pass)
  * [bhoriuchi/terraform-backend-http](https://github.com/bhoriuchi/terraform-backend-http)
* git
  * [plumber-cd/terraform-backend-git](https://github.com/plumber-cd/terraform-backend-git)

## Automation

### Build (CI/CD pipelines)

GitHub Actions are triggered to automate the integration and delivery of the application:

- [CI](.github/workflows/ci.yaml)
- [PKG](.github/workflows/pkg.yaml)

GitHub project has been configured, in **General** / **Security** / **Secrets and Variables** / **Actions**:

- DOCKERHUB_TOKEN
- DOCKERHUB_USERNAME
- SONAR_HOST_URL
- SONAR_ORG
- SONAR_PROJECT_KEY
- SONAR_TOKEN

## Procedures

### Run locally the application

Create/have a MongoDB database (example with a local container but you can provision a cluster in MongoDB Atlas):

```bash
# creates a container
docker run --name mongodb -d -p 27017:27017 mongo:8.0
# (optional) adds indexes for optimal performances
docker run --rm --link mongodb \
  -v "$(pwd)/scripts":/home/scripts mongo:8.0 \
  bash -c "mongo mongodb://mongodb:27017/terraform_backend_dev /home/scripts/mongo-create-index.js"
```

Run the web API (example with the command line but an IDE like Visual Studio or Rider would be nice to be able to debug):

```bash
dotnet run --project src/WebApi
```

Open Swagger in a browser: [localhost:5293/swagger](http://localhost:5293/swagger).
