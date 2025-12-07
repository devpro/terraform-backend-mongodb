# Quickstart

Let's do a quick demonstration of the application.

## Make sure requirements are met

[Terraform](https://developer.hashicorp.com/terraform/install), or [OpenTofu](https://opentofu.org/docs/intro/install/), must be available from the command line.

## Run the application and the database in containers

=== "Docker"

    ```bash
    curl -O https://raw.githubusercontent.com/devpro/terraform-backend-mongodb/refs/heads/main/compose.yaml
    docker compose up
    ```

<!-- 
TODO: add other options

=== "Podman"

    ```bash
    
    ```

=== "Podman"

    ```bash
    helm upgrade --install tfbackend \
      https://github.com/devpro/helm-charts/releases/download/terraform-backend-mongodb-0.1.0/terraform-backend-mongodb-0.1.0.tgz \
      --create-namespace --namespace tfbackend
    ```

-->

## Create a user to authenticate calls

=== "Docker"

    ```bash
    curl -O https://raw.githubusercontent.com/devpro/terraform-backend-mongodb/refs/heads/main/scripts/tfbeadm
    MONGODB_CONTAINERNAME=tfbackmdb-mongodb-1 MONGODB_CONTAINERNETWORK=tfbackmdb_default tfbeadm create-user admin admin123 dummy
    ```

## Create Terraform files

=== "Docker"

    ```bash
    curl -O https://raw.githubusercontent.com/devpro/terraform-backend-mongodb/refs/heads/main/samples/local-files/main.tf
    ```

## Initiatize Terraform

```bash
terraform init
```

## Create the resources

```bash
terraform apply
```

## Query the state database

=== "Docker"

    ```bash
    docker run --rm --link "tfbackmdb-mongodb-1" --network "tfbackmdb_default" "mongo:8.2" \
      bash -c "mongosh \"mongodb://mongodb:27017/terraform_backend_dev\" --eval 'db.tf_state.find().projection({tenant: 1, name: 1, createdAt: 1, \"value.version\": 1, \"value.resources.type\": 1, \"value.resources.name\": 1})'"
    ```

## Destroy the resources

```bash
terraform destroy
```
