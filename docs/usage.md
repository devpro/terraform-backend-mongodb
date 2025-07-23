# Usage

## Tenant authentication

API calls are secured through tenant isolation and user authentication, which are stored in the MongoDB database.

You can add a user with the following script (replace the parameters):

```bash
./scripts/mongo-create-user.sh myusername mypassword mytenant
docker run --rm --link mongodb \
  -v "$(pwd)/scripts":/home/scripts mongo:8.0 \
  bash -c "mongosh mongodb://<dbserver>:<dbport>/<dbname> /home/scripts/add-user.js"
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

And now, you can take advantage of a MongoDB backend for your Terraform/OpenTofu actions!
