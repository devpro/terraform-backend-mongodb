# Setup

## Requirements

### Database server

The application needs a MongoDB database that can be hosted in a:

- MongoDB Cluster managed by Atlas (easiest solution, free tier available)
- MongoDB Replica Set running from release binaries on one or multiple servers
- MongoDB Replica Set running in containers from MongoDB container image (available on DockerHub)
- Kubernetes cluster using MongoDB Community or Enterprise Kubernetes Operator

Once the database is available, grab the connection string for a user with admin permissions.

!!! tip

    Double check any network/security restrictions such as MongoDB IP access list as the application needs to access the MongoDB server

### Database indexes

Add indexes for optimal performances:

=== "Commands"

    ```js
    db.tf_state.createIndex({"tenant": 1, "name": 1})
    db.tf_state_lock.createIndex({"tenant": 1, "name": 1}, {unique: true})
    db.user.createIndex({"username": 1}, {unique: true})
    ```

=== "Script"

    ```bash
    curl -O https://raw.githubusercontent.com/devpro/terraform-backend-mongodb/refs/heads/main/scripts/tfbeadm
    MONGODB_URI=mongodb://<myserver>:27017/<mydb> tfbeadm create-indexes
    ```

    !!! warning

        `mongosh` or `Docker` must be available on the machine running the commands

## Installation

### Kubernetes

Use the official Helm chart:

```bash
# adds the chart repository
helm repo add devpro https://devpro.github.io/helm-charts
helm repo update

# installs the chart
helm upgrade --install tfbackend devpro/terraform-backend-mongodb [-f values.yaml] --create-namespace --namespace tfbackend
```

Values file examples:

=== "Traefik Ingress with Let's Encrypt cert-manager issuer"

    ```yaml
    webapi:
      host: tfbackend.mydomain
    ingress:
      enabled: true
      className: traefik
      annotations:
        cert-manager.io/cluster-issuer: letsencrypt-prod
    ```

=== "Development environment with Swagger"

    ```yaml
    dotnet:
      environment: Development
      enableSwagger: true
      enableOpenTelemetry: false
    ```

=== "Embedded MongoDB chart"

    ```yaml
    mongodb:
      enabled: true
      auth:
        rootPassword: admin
    webapi:
      db:
        connectionString: mongodb://root:admin@tfbackend-mongodb:27017/terraform_backend_beta?authSource=admin
        databaseName: terraform_backend_beta
    ```
