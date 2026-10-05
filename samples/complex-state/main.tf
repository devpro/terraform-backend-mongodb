terraform {
  backend "http" {
    lock_method            = "POST"
    unlock_method          = "DELETE"
    skip_cert_verification = "true"
  }
}

variable "revision" {
  type    = number
  default = 1
}

# terraform_data ships with Terraform itself, so this needs no provider credentials.
# Its "input" attribute is typed any, so whatever shape is assigned here lands in the state exactly as written, which is what a real provider attribute does and no C# class in this repository models.
locals {
  complex_payload = {
    metadata = {
      environment = "production"
      region      = "eu-west-3"
      revision    = var.revision
      tags        = ["billing:platform-eng", "critical", "pii=false"]
    }
    network = {
      cidr_blocks = ["10.0.0.0/16", "10.1.0.0/16"]
      subnets = [
        { name = "public-a", az = "eu-west-3a", public = true, capacity = 254 },
        { name = "private-a", az = "eu-west-3a", public = false, capacity = 65534 },
      ]
      enabled          = true
      disabled_feature = null
    }
    scaling = {
      min        = 1
      max        = 50
      target     = 12.5
      # beyond Int64, produced by a real apply rather than a constructed payload
      identifier = 123456789012345678901234567890
    }
  }
}

resource "terraform_data" "complex_state" {
  input = local.complex_payload
}

resource "null_resource" "test_backend" {
  provisioner "local-exec" {
    command = "echo 'Testing HTTP backend state management'"
  }
}

resource "local_file" "test" {
  content  = "Test HTTP backend"
  filename = "${path.module}/temp.txt"
}

resource "random_string" "test" {
  length = 16
}
