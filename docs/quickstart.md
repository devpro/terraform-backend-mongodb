# Quickstart

## Demo

This is a complete walkthrough to see Terraform Backend MongoDB in action.

<iframe width="800" height="450" src="https://www.youtube.com/embed/6KuqT6DGw7Y?si=eQCfXgzKoOgkddX1" title="YouTube video player"
    frameborder="0" allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share"
    referrerpolicy="strict-origin-when-cross-origin" allowfullscreen></iframe>

1. Make sure the following tools are available from the command line:

    - [Docker](https://docs.docker.com/engine/install/), [Podman](https://podman.io/docs/installation) with a Compose provider, or [kubectl](https://kubernetes.io/docs/tasks/tools/) and [Helm](https://helm.sh/docs/intro/install/) with a Kubernetes cluster
    - [Terraform](https://developer.hashicorp.com/terraform/install) or [OpenTofu](https://opentofu.org/docs/intro/install/)

2. Run the application and the database in containers:

    === "Docker"

        ```bash
        curl -O https://raw.githubusercontent.com/devpro/terraform-backend-mongodb/refs/heads/main/compose.yaml
        curl --create-dirs -o demo/otel-collector-config.yaml https://raw.githubusercontent.com/devpro/terraform-backend-mongodb/refs/heads/main/demo/otel-collector-config.yaml
        docker compose up
        ```

    === "Podman"

        ```bash
        curl -O https://raw.githubusercontent.com/devpro/terraform-backend-mongodb/refs/heads/main/compose.yaml
        curl --create-dirs -o demo/otel-collector-config.yaml https://raw.githubusercontent.com/devpro/terraform-backend-mongodb/refs/heads/main/demo/otel-collector-config.yaml
        podman compose up
        ```

    === "Kubernetes"

        ```bash
        helm upgrade --install tfbackend terraform-backend-mongodb --repo https://devpro.github.io/helm-charts \
          --set webapi.tag=latest --set mongodb.enabled=true --set mongodb.auth.rootPassword=<rootpassword> --set dotnet.enableScalar=true \
          --create-namespace --namespace tfbackend --wait
        kubectl port-forward svc/tfbackend 9001:80 --namespace tfbackend
        ```

    The REST API definitions can be viewed and tried through the Scalar web page on [localhost:9001](http://localhost:9001/scalar)

3. Create the indexes and a user to authenticate calls, whose password is prompted for:

    === "Docker"

        ```bash
        curl -O https://raw.githubusercontent.com/devpro/terraform-backend-mongodb/refs/heads/main/scripts/tfbeadm
        docker run --rm -it --network tfbackmdb_default -v "$PWD/tfbeadm:/tfbeadm:ro" mongo:8.2 bash -c \
          "apt-get update -qq && apt-get install -qq -y apache2-utils > /dev/null && /tfbeadm create-indexes && /tfbeadm create-user admin dummy"
        ```

    === "Podman"

        ```bash
        curl -O https://raw.githubusercontent.com/devpro/terraform-backend-mongodb/refs/heads/main/scripts/tfbeadm
        podman run --rm -it --network tfbackmdb_default -v "$PWD/tfbeadm:/tfbeadm:ro,Z" docker.io/library/mongo:8.2 bash -c \
          "apt-get update -qq && apt-get install -qq -y apache2-utils > /dev/null && /tfbeadm create-indexes && /tfbeadm create-user admin dummy"
        ```

    === "Kubernetes"

        `htpasswd` must be available, from the `apache2-utils` or `httpd-tools` package:

        ```bash
        curl -O https://raw.githubusercontent.com/devpro/terraform-backend-mongodb/refs/heads/main/scripts/tfbeadm && chmod +x ./tfbeadm
        export MONGODB_KUBEDEPLOYNAME=deploy/tfbackend-mongodb
        export MONGODB_URI="mongodb://root:<rootpassword>@localhost:27017/tfbackend?authSource=admin"
        ./tfbeadm create-indexes
        ./tfbeadm create-user admin dummy
        ```

4. Write Terraform files from a pre-configured sample:

    ```bash
    curl -O https://raw.githubusercontent.com/devpro/terraform-backend-mongodb/refs/heads/main/samples/local-files/main.tf
    ```

5. Prepare the working directory for use with Terraform, with the password of the user created above:

    ```bash
    export TF_HTTP_ADDRESS="http://localhost:9001/dummy/state/quickstart"
    export TF_HTTP_LOCK_ADDRESS="$TF_HTTP_ADDRESS/lock"
    export TF_HTTP_UNLOCK_ADDRESS="$TF_HTTP_ADDRESS/lock"
    export TF_HTTP_USERNAME="admin"
    export TF_HTTP_PASSWORD="<password>"
    terraform init
    ```

6. Perform the operations indicated in the Terraform project files:

    ```bash
    terraform apply
    ```

7. Query the state database to see the Terraform state information:

    === "Docker"

        ```bash
        docker run --rm --network "tfbackmdb_default" "mongo:8.2" \
          bash -c "mongosh \"mongodb://mongodb:27017/tfbackend_dev\" --eval 'db.tf_state.find().projection({tenant: 1, name: 1, created_at: 1, \"value.version\": 1, \"value.resources.type\": 1, \"value.resources.name\": 1})'"
        ```

    === "Podman"

        ```bash
        podman run --rm --network "tfbackmdb_default" "docker.io/library/mongo:8.2" \
          bash -c "mongosh \"mongodb://mongodb:27017/tfbackend_dev\" --eval 'db.tf_state.find().projection({tenant: 1, name: 1, created_at: 1, \"value.version\": 1, \"value.resources.type\": 1, \"value.resources.name\": 1})'"
        ```

    === "Kubernetes"

        ```bash
        kubectl exec deploy/tfbackend-mongodb --namespace tfbackend -- \
          bash -c "mongosh \"$MONGODB_URI\" --eval 'db.tf_state.find().projection({tenant: 1, name: 1, created_at: 1, \"value.version\": 1, \"value.resources.type\": 1, \"value.resources.name\": 1})'"
        ```

8. Destroy the resources that were created with Terraform:

    ```bash
    terraform destroy
    ```
