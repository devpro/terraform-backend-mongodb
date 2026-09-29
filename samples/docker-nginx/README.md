# Terraform Docker sample

This sample creates and manages an nginx container in Docker, inspired by [Terraform Get Started](https://developer.hashicorp.com/terraform/tutorials/docker-get-started).

## Setup

The following tools must be available from the command line:

- [.NET](https://dotnet.microsoft.com/download), or an IDE such as Visual Studio or Rider
- [Terraform](https://developer.hashicorp.com/terraform/install) or [OpenTofu](https://opentofu.org/docs/intro/install/), used interchangeably below
- Docker

## Workflow

Run the application, or start it from an IDE:

```bash
dotnet run --project src/WebApi
```

Initialize from the sample directory:

```bash
cd samples/docker-nginx
export TFBACKEND_URL="http://localhost:5293"  # 9001 with docker compose
export TF_HTTP_ADDRESS="$TFBACKEND_URL/dummy/state/docker-nginx"
export TF_HTTP_LOCK_ADDRESS="$TFBACKEND_URL/dummy/state/docker-nginx/lock"
export TF_HTTP_UNLOCK_ADDRESS="$TFBACKEND_URL/dummy/state/docker-nginx/lock"
export TF_HTTP_USERNAME="admin"
export TF_HTTP_PASSWORD="<password>"
terraform init -reconfigure
```

Apply the change.
While `terraform apply` waits for confirmation, the state is locked, and an apply from a second terminal is refused:

```bash
terraform apply
```

Check the running container:

```bash
docker ps
curl localhost:8000
```

Destroy resources:

```bash
terraform destroy
```
