# Terraform Docker sample

This sample will create and manage a container in Docker using Terraform and our MongoDB HTTP backend.
It is inspired from [Terraform Get Started](https://learn.hashicorp.com/collections/terraform/docker-get-started).

## Setup

The following tools must be available from the command line:

- [.NET](https://dotnet.microsoft.com/download) or an IDE (Visual Studio or Rider)
- [Terraform](https://developer.hashicorp.com/terraform/install) or [OpenTofu](https://opentofu.org/docs/intro/install/)
- Docker

## Workflow

Run the application (example given for information but feel free to start from the IDE):

```bash
dotnet run --project src/WebApi
```

Go to the sample directory:

```bash
cd samples/docker-nginx
```

Create, if not already present, an `.env` file:

<!-- port can be 5293 (project run from VS) or 9001 (docker compose)
export TFBACKEND_URL="https://localhost:7293/" Visual Studio debug session but issue with accessing from WSL
-->

```bash
export TF_LOG=TRACE
export TFBACKEND_TENANT="dummy"
export ENVIRONMENT="docker-nginx-main"
export TFBACKEND_URL="http://localhost:5293" # Visual Studio debug session
export TF_HTTP_ADDRESS="${TFBACKEND_URL}/${TFBACKEND_TENANT}/state/docker-nginx-${ENVIRONMENT}"
export TF_HTTP_LOCK_ADDRESS="${TFBACKEND_URL}/${TFBACKEND_TENANT}/state/docker-nginx-${ENVIRONMENT}/lock"
export TF_HTTP_UNLOCK_ADDRESS="${TFBACKEND_URL}/${TFBACKEND_TENANT}/state/docker-nginx-${ENVIRONMENT}/lock"
export TF_HTTP_USERNAME="admin"
export TF_HTTP_PASSWORD="admin123"
```

Set Terraform environment variable:

```bash
source .env
```

Initialize (feel free to use `tofu` instead of `terraform`):

```bash
terraform init
```

Apply the change (before confirming you can check from on another terminal to run an apply):

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
