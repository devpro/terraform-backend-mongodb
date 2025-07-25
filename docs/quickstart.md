# Quickstart

## Demo

Before looking at all the options, let's do a quick demonstration of the application.

=== "Containers (Docker Compose)"

    ```bash
    docker compose
    ```

=== "Kubernetes (Helm)"

    ```bash
    helm upgrade --install tfbackend https://github.com/devpro/helm-charts/releases/download/terraform-backend-mongodb-0.1.0/terraform-backend-mongodb-0.1.0.tgz --create-namespace --namespace tfbackend
    ```

## Samples

* [Make local actions on files](samples/local-files/README.md)
* [Run NGINX container with Docker](samples/docker-nginx/README.md)
