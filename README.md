# Terraform backend management in MongoDB

Store [Terraform](https://www.terraform.io) state in [MongoDB](https://www.mongodb.com/), thanks to this
[HTTP](https://www.terraform.io/language/settings/backends/http) [backend](https://github.com/hashicorp/terraform/tree/main/internal/backend/remote-state).

## Samples with other technologies

* [GitLab managed Terraform State](https://gitlab.com/gitlab-org/manage/import/gitlab/-/blob/master/doc/user/infrastructure/terraform_state.md)
  * [lib/api/terraform/state.rb](https://gitlab.com/gitlab-org/manage/import/gitlab/-/blob/master/lib/api/terraform/state.rb)
* [bhoriuchi/terraform-backend-http](https://github.com/bhoriuchi/terraform-backend-http)
* [plumber-cd/terraform-backend-git](https://github.com/plumber-cd/terraform-backend-git)
