# Terraform backend management in MongoDB

Store [Terraform](https://www.terraform.io) state in [MongoDB](https://www.mongodb.com/), using
[HTTP](https://www.terraform.io/language/settings/backends/http) [backend](https://github.com/hashicorp/terraform/tree/main/internal/backend/remote-state).

## How to use

* Run the web API

* Update the Terraform file

```tf
terraform {
  backend "http" {
    address = "<webapi_url>/state/<project_name>"
    lock_address = "<webapi_url>/state/<project_name>/lock"
    unlock_address = "<webapi_url>/state/<project_name>/lock"
    lock_method = "POST"
    unlock_method = "DELETE"
    # uncomment if HTTPS certificate is not valid
    # skip_cert_verification = "true"
  }
}
```

* Execute usual Terraform command lines

## How to evaluate

* Run the [samples](samples/README.md)

## How to compare

### Samples with other solutions

* [GitLab](https://gitlab.com/gitlab-org/manage/import/gitlab/-/blob/master/doc/user/infrastructure/terraform_state.md)
  * [lib/api/terraform/state.rb](https://gitlab.com/gitlab-org/manage/import/gitlab/-/blob/master/lib/api/terraform/state.rb)
* HTTP
  * [akshay/terraform-http-backend-pass](https://git.coop/akshay/terraform-http-backend-pass)
  * [bhoriuchi/terraform-backend-http](https://github.com/bhoriuchi/terraform-backend-http)
* git
  * [plumber-cd/terraform-backend-git](https://github.com/plumber-cd/terraform-backend-git)
