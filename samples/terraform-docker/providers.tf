terraform {
  required_version = ">= 1.1.0"

  # https://developer.hashicorp.com/terraform/language/settings/backends/http#configuration-variables
  backend "http" {
    # port can be 5293 (project run from VS) or 9001 (docker compose)
    address                = "http://localhost:5293/state/demo_devpro"
    lock_address           = "http://localhost:5293/state/demo_devpro/lock"
    unlock_address         = "http://localhost:5293/state/demo_devpro/lock"
    lock_method            = "POST"
    unlock_method          = "DELETE"
    username               = "admin"
    password               = "admin"
    skip_cert_verification = "true"
  }

  # https://registry.terraform.io/providers/kreuzwerker/docker/latest
  required_providers {
    docker = {
      source = "kreuzwerker/docker"
      version = "~> 3.0.1"
    }
  }
}

provider "docker" {}
