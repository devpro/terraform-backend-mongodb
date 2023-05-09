# Terraform backend management in MongoDB

[![CI](https://github.com/devpro/terraform-backend-mongodb/actions/workflows/ci.yaml/badge.svg?branch=main)](https://github.com/devpro/terraform-backend-mongodb/actions/workflows/ci.yaml)
[![PKG](https://github.com/devpro/terraform-backend-mongodb/actions/workflows/pkg.yaml/badge.svg?branch=main)](https://github.com/devpro/terraform-backend-mongodb/actions/workflows/pkg.yaml)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=devpro.terraform-backend-mongodb&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=devpro.terraform-backend-mongodb)
[![Docker Image Version](https://img.shields.io/docker/v/devprofr/terraform-backend-mongodb?label=Image)](https://hub.docker.com/r/devprofr/terraform-backend-mongodb)

Store [Terraform](https://www.terraform.io) state in [MongoDB](https://www.mongodb.com/), using
[HTTP](https://www.terraform.io/language/settings/backends/http) [backend](https://github.com/hashicorp/terraform/tree/main/internal/backend/remote-state).

## How to use

* Create a MongoDB database (you can provision a cluster in MongoDB Atlas)

```bash
# example on a MongoDB container running locally
docker run --name mongodb -d -p 27017:27017 mongo:5.0
```

* Run the web API

* Update the Terraform file

```tf
terraform {
  backend "http" {
    address                = "<webapi_url>/state/<project_name>"
    lock_address           = "<webapi_url>/state/<project_name>/lock"
    unlock_address         = "<webapi_url>/state/<project_name>/lock"
    lock_method            = "POST"
    unlock_method          = "DELETE"
    username               = "<api_username>"
    password               = "<api_password>"
    # uncomment if HTTPS certificate is not valid
    # skip_cert_verification = "true"
  }
}
```

* Execute usual Terraform command lines

* (Optional) Add MongoDB indexes for optimal performances

```bash
# example on a MongoDB container running locally
docker run --rm --link mongodb \
  -v "$(pwd)/scripts":/home/scripts mongo:5.0 \
  bash -c "mongo mongodb://mongodb:27017/terraform_backend_dev /home/scripts/mongo-create-index.js"
```

## How to demonstrate

* Run the [terraform-docker sample](samples/terraform-docker/README.md)

## How to contribute

This is a .NET 7 / C# codebase (open-source, cross-platform, free, object-oriented technologies)

### Project structure

Project name | Technology | Project type
------------ | ---------- | ------------
`Common.AspNetCore` | .NET 7 | Library
`Common.MongoDb` | .NET 7 | Library
`Common.Runtime` | .NET 7 | Library
`Domain` | .NET 7 | Library
`Infrastructure.MongoDb` | .NET 7 | Library
`WebApi` | ASP.NET 7 | Web application (REST API)

### Packages (NuGet)

Name | Description
---- | -----------
`MongoDB.Bson`, `MongoDB.Driver` | MongoDB .NET Driver
`Swashbuckle.AspNetCore` | OpenAPI / Swagger generation
`System.Text.Json` | JSON support

## How to compare

### Samples with other solutions

* [GitLab](https://gitlab.com/gitlab-org/manage/import/gitlab/-/blob/master/doc/user/infrastructure/terraform_state.md)
  * [lib/api/terraform/state.rb](https://gitlab.com/gitlab-org/manage/import/gitlab/-/blob/master/lib/api/terraform/state.rb)
* HTTP
  * [akshay/terraform-http-backend-pass](https://git.coop/akshay/terraform-http-backend-pass)
  * [bhoriuchi/terraform-backend-http](https://github.com/bhoriuchi/terraform-backend-http)
* git
  * [plumber-cd/terraform-backend-git](https://github.com/plumber-cd/terraform-backend-git)
