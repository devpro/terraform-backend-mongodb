terraform {
  backend "http" {
    address                = "http://localhost:5293/sample/state/local-files"
    lock_address           = "http://localhost:5293/sample/state/local-files/lock"
    unlock_address         = "http://localhost:5293/sample/state/local-files/lock"
    lock_method            = "POST"
    unlock_method          = "DELETE"
    username               = "admin"
    password               = "admin"
    skip_cert_verification = "true"
  }
}

resource "null_resource" "test_backend" {
  provisioner "local-exec" {
    command = "echo 'Testing HTTP backend state management' > test.txt"
  }
}

resource "local_file" "test" {
  content  = "Test HTTP backend"
  filename = "${path.module}/temp.txt"
}

resource "random_string" "test" {
  length = 16
}
