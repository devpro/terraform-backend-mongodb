# Usage

## Tenant authentication

API calls are secured through tenant isolation and user authentication, which are stored in the MongoDB database.

Users must be added to the database with this script:

```bash
curl -O https://raw.githubusercontent.com/devpro/terraform-backend-mongodb/refs/heads/main/scripts/tfbeadm
MONGODB_URI=mongodb://<myserver>:27017/<mydb> tfbeadm create-user <myusername> <mypassword> <mytenant>
```

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

You're now ready!
