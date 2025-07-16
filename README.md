# MongoDB backend for Terraform/OpenTofu state

[![CI](https://github.com/devpro/terraform-backend-mongodb/actions/workflows/ci.yaml/badge.svg?branch=main)](https://github.com/devpro/terraform-backend-mongodb/actions/workflows/ci.yaml)
[![PKG](https://github.com/devpro/terraform-backend-mongodb/actions/workflows/pkg.yaml/badge.svg?branch=main)](https://github.com/devpro/terraform-backend-mongodb/actions/workflows/pkg.yaml)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=devpro_terraform-backend-mongodb&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=devpro_terraform-backend-mongodb)
[![Docker Image Version](https://img.shields.io/docker/v/devprofr/terraform-backend-mongodb?label=Image&logo=docker)](https://hub.docker.com/r/devprofr/terraform-backend-mongodb)

Store Terraform/OpenTofu state in a MongoDB database thanks to his HTTP backend.

Look at the [project development guide](CONTRIBUTING.md) for more technical details.
You're more than welcome to contribute!

## Quick start

1. Make sure a you have access to a MongoDB database

2. Configure the application with the MongoDB database connection information

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

* [Execute local actions](samples/terraform-local/README.md)
* [Manage Docker images](samples/terraform-docker/README.md)
