terraform {
  backend "http" {
    address                = "http://localhost:5293/state/demo_localexec"
    lock_address           = "http://localhost:5293/state/demo_localexec/lock"
    unlock_address         = "http://localhost:5293/state/demo_localexec/lock"
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
