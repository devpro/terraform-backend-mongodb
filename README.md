# Terraform backend management in MongoDB

Store [Terraform](https://www.terraform.io) state in [MongoDB](https://www.mongodb.com/), using
[HTTP](https://www.terraform.io/language/settings/backends/http) [backend](https://github.com/hashicorp/terraform/tree/main/internal/backend/remote-state).

## How to use

* Run the web API

* Update the Terraform file

```tf
terraform {
  backend "http" {
    address = "<webapi_url>/state/<project_name>"
    lock_address = "<webapi_url>/state/<project_name>/lock"
    unlock_address = "<webapi_url>/state/<project_name>/lock"
    lock_method = "POST"
    unlock_method = "DELETE"
    # uncomment if HTTPS certificate is not valid
    # skip_cert_verification = "true"
  }
}
```

* Execute usual Terraform command lines

## How to demonstrate

* Run the [samples](samples/README.md)

## How to contribute

This is a .NET 6 / C# codebase (open-source, cross-platform, free, object-oriented technologies)

### Project structure

Project name | Technology | Project type
------------ | ---------- | ------------
`Common.AspNetCore` | .NET 6 | Library
`Common.MongoDb` | .NET Standard 2.1 | Library
`Common.Runtime` | .NET Standard 2.1 | Library
`Domain` | .NET Standard 2.1 | Library
`Infrastructure.MongoDb` | .NET Standard 2.1 | Library
`WebApi` | ASP.NET 6 | Web application (REST API)

### Packages (NuGet)

Name | Description
---- | -----------
`MongoDB.Bson`, `MongoDB.Driver`, `MongoDB.Driver.Core` | MongoDB .NET Driver
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
