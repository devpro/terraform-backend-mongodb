# Usage

## Tenant authentication

API calls are secured through tenant isolation and user authentication, which are stored in the MongoDB database.
User passwords are hashed with BCrypt.

`tfbeadm` script is the easiest way to create the users correctly:

```bash
curl -O https://raw.githubusercontent.com/devpro/terraform-backend-mongodb/refs/heads/main/scripts/tfbeadm && chmod +x ./tfbeadm
MONGODB_URI=mongodb://<myserver>:27017/<mydb> ./tfbeadm create-user <myusername> <mytenant>
```

The password is read from a prompt, so it is never written to the shell history and never appears in the process list.
To create the account without a prompt, pipe the password in:

```bash
openssl rand -base64 24 | MONGODB_URI=mongodb://<myserver>:27017/<mydb> ./tfbeadm create-user <myusername> <mytenant>
```

A generated credential is strongly preferred over a chosen one.
Terraform sends this password on every request, and it is the only thing standing between the internet and the state of every workspace in the tenant, so it should be long and random and kept in a secret manager.
The form taking the password as a third argument still works and is deprecated, because an argument is visible to every user on the host for the lifetime of the command.

## Client configuration

In the tf file, configure the backend to use the REST API:

```tf
terraform {
  backend "http" {
    address                = "http://<api-url>/<mytenant>/state/<project>"
    lock_address           = "http://<api-url>/<mytenant>/state/<project>/lock"
    unlock_address         = "http://<api-url>/<mytenant>/state/<project>/lock"
    lock_method            = "POST"
    unlock_method          = "DELETE"
    username               = "<myusername>"
    password               = "<mypassword>"
    #skip_cert_verification = "true"
  }
}
```
