# Sample with local commands

## Setup

The following tools must be available from the command line:

- [.NET](https://dotnet.microsoft.com/download) or an IDE (Visual Studio or Rider)
- [Terraform](https://developer.hashicorp.com/terraform/install) or [OpenTofu](https://opentofu.org/docs/intro/install/)

## Workflow

Run the application (example given for information but feel free to start from the IDE):

```bash
dotnet run --project src/WebApi
```

Go to the sample directory:

```bash
cd samples/terraform-local-exec
```

Initialize (feel free to use `tofu` instead of `terraform`):

```bash
export TFBACKEND_URL="http://localhost:5293"
# export TFBACKEND_URL="http://localhost:9001"
export TF_HTTP_ADDRESS="$TFBACKEND_URL/dummy/state/local-files"
export TF_HTTP_LOCK_ADDRESS="$TFBACKEND_URL/dummy/state/local-files/lock"
export TF_HTTP_UNLOCK_ADDRESS="$TFBACKEND_URL/dummy/state/local-files/lock"
export TF_HTTP_USERNAME="admin"
export TF_HTTP_PASSWORD="admin123"
terraform init -reconfigure
```

Apply the change (before confirming you can check from on another terminal to run an apply):

```bash
terraform apply
```

Destroy resources:

```bash
terraform destroy
```
