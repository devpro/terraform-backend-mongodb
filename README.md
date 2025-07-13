# Terraform backend management in MongoDB

[![CI](https://github.com/devpro/terraform-backend-mongodb/actions/workflows/ci.yaml/badge.svg?branch=main)](https://github.com/devpro/terraform-backend-mongodb/actions/workflows/ci.yaml)
[![PKG](https://github.com/devpro/terraform-backend-mongodb/actions/workflows/pkg.yaml/badge.svg?branch=main)](https://github.com/devpro/terraform-backend-mongodb/actions/workflows/pkg.yaml)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=devpro_terraform-backend-mongodb&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=devpro_terraform-backend-mongodb)
[![Docker Image Version](https://img.shields.io/docker/v/devprofr/terraform-backend-mongodb?label=Image&logo=docker)](https://hub.docker.com/r/devprofr/terraform-backend-mongodb)

Store [Terraform](https://www.terraform.io) state in [MongoDB](https://www.mongodb.com/), using
[HTTP](https://www.terraform.io/language/settings/backends/http) [backend](https://github.com/hashicorp/terraform/tree/main/internal/backend/remote-state).

Look at the [project development guide](CONTRIBUTING.md) for more technical details. You're more than welcome to contribute!

## Quick start

1. Create a MongoDB database (example with a local container but you can provision a cluster in MongoDB Atlas)

```bash
# starts the container
docker run --name mongodb -d -p 27017:27017 mongo:8.0
# (optional) adds indexes for optimal performances
docker run --rm --link mongodb \
  -v "$(pwd)/scripts":/home/scripts mongo:8.0 \
  bash -c "mongo mongodb://mongodb:27017/terraform_backend_dev /home/scripts/mongo-create-index.js"
```

2. Run the web API

3. Update the Terraform file

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

4. Execute usual Terraform command lines

## Samples

* [Docker](samples/terraform-docker/README.md)

## Alternatives & references

### Terraform backend implementations

* [GitLab](https://gitlab.com/gitlab-org/manage/import/gitlab/-/blob/master/doc/user/infrastructure/terraform_state.md)
  * [lib/api/terraform/state.rb](https://gitlab.com/gitlab-org/manage/import/gitlab/-/blob/master/lib/api/terraform/state.rb)
* HTTP
  * [akshay/terraform-http-backend-pass](https://git.coop/akshay/terraform-http-backend-pass)
  * [bhoriuchi/terraform-backend-http](https://github.com/bhoriuchi/terraform-backend-http)
* git
  * [plumber-cd/terraform-backend-git](https://github.com/plumber-cd/terraform-backend-git)
