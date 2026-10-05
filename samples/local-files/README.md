# Sample with local files

This sample creates a local file, a random string and a null resource, so it needs no credentials and no infrastructure.

## Setup

The following tools must be available from the command line:

- [.NET](https://dotnet.microsoft.com/download), or an IDE such as Visual Studio or Rider
- [Terraform](https://developer.hashicorp.com/terraform/install) or [OpenTofu](https://opentofu.org/docs/intro/install/), used interchangeably below

MongoDB must be running with the indexes and a user `admin` in the tenant `dummy`, as described in [Debug the application](../../CONTRIBUTING.md#debug-the-application).

## Workflow

Run the application, or start it from an IDE:

```bash
dotnet run --project src/WebApi
```

Initialize from the sample directory:

```bash
cd samples/local-files
export TFBACKEND_URL="http://localhost:5293"  # 9001 with docker compose
export TF_HTTP_ADDRESS="$TFBACKEND_URL/dummy/state/local-files"
export TF_HTTP_LOCK_ADDRESS="$TFBACKEND_URL/dummy/state/local-files/lock"
export TF_HTTP_UNLOCK_ADDRESS="$TFBACKEND_URL/dummy/state/local-files/lock"
export TF_HTTP_USERNAME="admin"
export TF_HTTP_PASSWORD="<password>"
terraform init -reconfigure
```

Apply the change.
While `terraform apply` waits for confirmation, the state is locked, and an apply from a second terminal is refused:

```bash
terraform apply
```

Destroy resources:

```bash
terraform destroy
```
