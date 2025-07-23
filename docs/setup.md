# Setup

## Requirements

### Database server

Make sure you have access to a MongoDB database (with a connection string containing a user that have admin permissions).

The MongoDB server can run:

- On a machine from the binary
- On multiple machines from the binary
- In a container
- In a Kubernetes cluster
- In MongoDB Atlas (free tier available!)

!!! warning

    Double check any network/security restrictions such as MongoDB IP access list as the application needs to access the MongoDB server

### Database indexes

Add indexes for optimal performances:

```bash
docker run --rm --link mongodb \
  -v "$(pwd)/scripts":/home/scripts mongo:8.0 \
  bash -c "mongosh mongodb://<dbserver>:<dbport>/<dbname> /home/scripts/mongo-create-index.js"
```

## Installation

### Kubernetes

Use the official Helm chart:

```bash
# adds the chart repository
helm repo add devpro https://devpro.github.io/helm-charts
helm repo update

# installs the chart
helm upgrade --install tfbackend devpro/terraform-backend-mongodb --create-namespace --namespace tfbackend
```
