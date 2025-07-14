# Terraform with local-exec

## Setup

Run the application, from the command line or the IDE.

[Terraform binary](https://developer.hashicorp.com/terraform/install) is the only other tool required on the machine.

## Workflow

Go to the sample directory:

```bash
cd samples\terraform-local-exec
```

Initialize:

```bash
terraform init
```

Apply the change (before confirming you can check from on another terminal to run an apply):

```bash
terraform apply
```

Destroy resources:

```bash
terraform destroy
```
