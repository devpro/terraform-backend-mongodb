# Samples

This is a very simple sample to experiment the Terraform backend.
It will create and manage a container in Docker (inspired from [Terraform Get Started](https://learn.hashicorp.com/collections/terraform/docker-get-started)).

## Demonstration

* Make sure docker runtime is running and can be accessed from the command line

```bash
docker ps
```

* Run the commands

```bash
cd samples

SET TF_LOG=TRACE

terraform init

terraform plan

terraform apply

# makes sure the container is running
docker ps

# get nginx container content
curl localhost:8000
```

* Update main.tf, apply the change and make sure the container is running ok (for example port 8000 -> 8080)

* Destroy the container

```bash
terraform destroy
```
