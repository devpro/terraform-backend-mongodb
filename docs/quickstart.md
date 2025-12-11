# Quickstart

## Demo

This is a complete walkthrough to see Terraform Backend MongoDB in action.

<iframe width="800" height="450" src="https://www.youtube.com/embed/6KuqT6DGw7Y?si=eQCfXgzKoOgkddX1" title="YouTube video player"
    frameborder="0" allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share"
    referrerpolicy="strict-origin-when-cross-origin" allowfullscreen></iframe>

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

[OpenTofu](https://opentofu.org/docs/intro/install/)
-->

1. Make sure the following tools are available from the commande line:

    - [Docker](https://docs.docker.com/engine/install/)
    - [Terraform](https://developer.hashicorp.com/terraform/install)

2. Run the application and the database in containers:

    === "Docker"

        ```bash
        curl -O https://raw.githubusercontent.com/devpro/terraform-backend-mongodb/refs/heads/main/compose.yaml
        docker compose up
        ```

    You can view and work with the REST API definitions through the Swagger web page on [localhost:9001](http://localhost:9001/swagger/index.html)

3. Create a user to authenticate calls:

    === "Docker"

        ```bash
        curl -O https://raw.githubusercontent.com/devpro/terraform-backend-mongodb/refs/heads/main/scripts/tfbeadm && chmod +x ./tfbeadm
        MONGODB_CONTAINERNAME=tfbackmdb-mongodb-1 MONGODB_CONTAINERNETWORK=tfbackmdb_default ./tfbeadm create-user admin admin123 dummy
        ```

4. Write Terraform files from a pre-configured sample:

    === "Docker"

        ```bash
        curl -O https://raw.githubusercontent.com/devpro/terraform-backend-mongodb/refs/heads/main/samples/local-files/main.tf
        ```

5. Prepare the working directory for use with Terraform:

    ```bash
    terraform init
    ```

6. Performs the operations indicated in Terraform project files:

    ```bash
    terraform apply
    ```

7. Query the state database to see the Terraform state information:

    === "Docker"

        ```bash
        docker run --rm --link "tfbackmdb-mongodb-1" --network "tfbackmdb_default" "mongo:8.2" \
          bash -c "mongosh \"mongodb://mongodb:27017/terraform_backend_dev\" --eval 'db.tf_state.find().projection({tenant: 1, name: 1, createdAt: 1, \"value.version\": 1, \"value.resources.type\": 1, \"value.resources.name\": 1})'"
        ```

8. Destroy the resources that were created with Terraform:

    ```bash
    terraform destroy
    ```
