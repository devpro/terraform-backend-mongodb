terraform {
  backend "http" {
    address                = "http://localhost:5293/state/demo_devpro"
    lock_address           = "http://localhost:5293/state/demo_devpro/lock"
    unlock_address         = "http://localhost:5293/state/demo_devpro/lock"
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
