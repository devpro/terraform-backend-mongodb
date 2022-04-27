terraform {
  required_version = ">= 1.1.0"

  backend "http" {
    address                = "http://localhost:5293/state/demo_kalosyni"
    lock_address           = "http://localhost:5293/state/demo_kalosyni/lock"
    unlock_address         = "http://localhost:5293/state/demo_kalosyni/lock"
    lock_method            = "POST"
    unlock_method          = "DELETE"
    skip_cert_verification = "true"
  }

  # https://registry.terraform.io/providers/kreuzwerker/docker/latest
  required_providers {
    docker = {
      source  = "kreuzwerker/docker"
      version = "2.16.0"
    }
  }
}

provider "docker" {}
