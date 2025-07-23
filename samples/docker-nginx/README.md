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
cd samples/terraform-docker
```

Initialize (feel free to use `tofu` instead of `terraform`):

```bash
SET TF_LOG=TRACE
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
