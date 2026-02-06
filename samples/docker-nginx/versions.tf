terraform {
  required_version = ">= 1.5"

  # https://developer.hashicorp.com/terraform/language/settings/backends/http#configuration-variables
  backend "http" {
    lock_method            = "POST"
    unlock_method          = "DELETE"
    skip_cert_verification = "true"
  }

  # https://registry.terraform.io/providers/kreuzwerker/docker/latest
  required_providers {
    docker = {
      source  = "kreuzwerker/docker"
      version = "3.6.2"
    }
  }
}
