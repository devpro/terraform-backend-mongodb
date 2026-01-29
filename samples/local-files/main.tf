terraform {
  backend "http" {
    lock_method            = "POST"
    unlock_method          = "DELETE"
    skip_cert_verification = "true"
  }
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
