# Sample with a complicated, dynamically-shaped state

## Purpose

This sample exists for `ComplexStateScenarioTest` in the integration suite.
It exercises the part of the storage contract that the other samples do not: a resource attribute whose shape is not fixed by any provider schema, and therefore not modeled by any class in this repository.

`terraform_data` ships with Terraform itself, so this needs no provider credentials.
Its `input` attribute is typed `any`, so the nested object assigned to it in `main.tf`, maps, lists of objects, booleans, a null, and an integer beyond `Int64`, lands in the state exactly as written, and nowhere else in this repository is that shape declared.

## Setup

The following tools must be available from the command line:

- [.NET](https://dotnet.microsoft.com/download), or an IDE such as Visual Studio or Rider
- [Terraform](https://developer.hashicorp.com/terraform/install) or [OpenTofu](https://opentofu.org/docs/intro/install/), used interchangeably below

## Workflow

Run the application, or start it from an IDE:

```bash
dotnet run --project src/WebApi
```

Initialize from the sample directory:

```bash
cd samples/complex-state
export TFBACKEND_URL="http://localhost:5293"  # 9001 with docker compose
export TF_HTTP_ADDRESS="$TFBACKEND_URL/dummy/state/complex-state"
export TF_HTTP_LOCK_ADDRESS="$TFBACKEND_URL/dummy/state/complex-state/lock"
export TF_HTTP_UNLOCK_ADDRESS="$TFBACKEND_URL/dummy/state/complex-state/lock"
export TF_HTTP_USERNAME="admin"
export TF_HTTP_PASSWORD="<password>"
terraform init -reconfigure
```

Apply the change:

```bash
terraform apply
```

Change the revision and apply again, to produce a `tf_state_history` entry:

```bash
terraform apply -var revision=2
```

Destroy resources:

```bash
terraform destroy -var revision=2
```
