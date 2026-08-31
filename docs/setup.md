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

The application does not set a connection pool size, so the driver default of 100 applies.
That is fine at ordinary scale, and it does not need a code change to raise: `maxPoolSize` is a standard connection string option, for example `mongodb://<host>/<db>?maxPoolSize=200`.

### Database indexes

Add indexes for optimal performances:

=== "Commands"

    ```js
    db.tf_state.createIndex({"tenant": 1, "name": 1})
    db.tf_state_lock.createIndex({"tenant": 1, "name": 1}, {unique: true})
    db.user.createIndex({"username": 1}, {unique: true})
    db.auth_lockout.createIndex({"username": 1, "remote_address": 1}, {unique: true})
    db.auth_lockout.createIndex({"expires_at": 1}, {expireAfterSeconds: 0})
    ```

=== "Script"

    ```bash
    curl -O https://raw.githubusercontent.com/devpro/terraform-backend-mongodb/refs/heads/main/scripts/tfbeadm
    MONGODB_URI=mongodb://<myserver>:27017/<mydb> tfbeadm create-indexes
    ```

    !!! warning

        `mongosh` or `Docker` must be available on the machine running the commands

### Upgrading from before the `created_at` rename

A deployment created before this change stores its state and history documents with a `createdAt` field.
The application itself now writes `created_at`, and every document a `terraform apply` touches from now on is rewritten with the new name automatically, so an upgrade needs nothing to keep working.
Run `tfbeadm migrate-created-at` to rename the field on every document immediately instead of waiting for it to be touched, for example before a read that lists states by `created_at`.
The command is safe to run more than once, and safe to run before or after the application itself is upgraded.

```bash
MONGODB_URI=mongodb://<myserver>:27017/<mydb> tfbeadm migrate-created-at
```

### Authentication and network settings

Six settings, introduced alongside the brute-force and proxy hardening, override with the `Section__Key` environment variable convention documented in `AGENTS.md`.

Setting | Environment variable | Default | Purpose
------- | --------------------- | ------- | -------
`Authentication:CredentialCacheSeconds` | `Authentication__CredentialCacheSeconds` | `60` | How long a verified credential skips BCrypt.
`Authentication:MaxFailedAttempts` | `Authentication__MaxFailedAttempts` | `10` | Consecutive failures, per username and source address, before that pair is refused.
`Authentication:LockoutSeconds` | `Authentication__LockoutSeconds` | `300` | How long a pair stays refused once locked out.
`Network:KnownProxies` | `Network__KnownProxies__0`, `__1`, ... | none | Reverse proxy addresses whose `X-Forwarded-For` header the application believes.
`Network:KnownNetworks` | `Network__KnownNetworks__0`, `__1`, ... | none | Same, as CIDR ranges.
`Network:TrustAllProxies` | `Network__TrustAllProxies` | `false` | Believes the forwarded headers of any caller, safe only where the application is reachable through the ingress alone.

!!! warning

    Behind a reverse proxy, at least one of `Network:KnownProxies`, `Network:KnownNetworks` or `Network:TrustAllProxies` must be set.
    Left unset, the application sees the proxy's own address on every request: every caller then shares a single lockout bucket, and every authentication-failure log entry names the proxy instead of the attacker.

## Installation

### Kubernetes

Add the Helm chart repository:

```bash
helm repo add devpro https://devpro.github.io/helm-charts
helm repo update
```

Create a values.yaml file with your configuration by looking at examples:

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

=== "Development environment with Scalar (OpenAPI web UI)"

    ```yaml
    dotnet:
      environment: Development
      enableScalar: true
      enableOpenTelemetry: false
    ```

=== "Embedded MongoDB chart"

    ```yaml
    mongodb:
      enabled: true
      auth:
        rootPassword: xxx
    webapi:
      db:
        connectionString: mongodb://root:xxx@tfbackend-mongodb:27017/tfbackend_beta?authSource=admin
        databaseName: tfbackend_beta
    ```

Install and manage the application with Helm:

```bash
helm upgrade --install tfbackend devpro/terraform-backend-mongodb -f values.yaml \
  --create-namespace --namespace tfbackend
```
