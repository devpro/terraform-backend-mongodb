# Setup

## Requirements

### Database server

The application needs a MongoDB database that can be hosted in a:

- MongoDB Cluster managed by Atlas (easiest solution, free tier available)
- MongoDB Replica Set running from release binaries on one or multiple servers
- MongoDB Replica Set running in containers from MongoDB container image (available on DockerHub)
- Kubernetes cluster using MongoDB Community or Enterprise Kubernetes Operator

Once the database is available, grab the connection string for a user with the `readWrite` role on the application database.

!!! tip

    Double check any network/security restrictions such as MongoDB IP access list as the application needs to access the MongoDB server

The application does not set a connection pool size, so the driver default of 100 applies.
That is fine at ordinary scale, and it does not need a code change to raise: `maxPoolSize` is a standard connection string option, for example `mongodb://<host>/<db>?maxPoolSize=200`.

### Database indexes

Add the indexes, the unique ones on `tf_state` and `tf_state_lock` being what makes a state write and a lock atomic:

=== "Commands"

    ```js
    db.tf_state.createIndex({"tenant": 1, "name": 1}, {unique: true})
    db.tf_state_history.createIndex({"tenant": 1, "name": 1})
    db.tf_state_lock.createIndex({"tenant": 1, "name": 1}, {unique: true})
    db.user.createIndex({"username": 1}, {unique: true})
    db.auth_lockout.createIndex({"username": 1, "remote_address": 1}, {unique: true})
    db.auth_lockout.createIndex({"expires_at": 1}, {expireAfterSeconds: 0})
    ```

=== "Script"

    ```bash
    curl -O https://raw.githubusercontent.com/devpro/terraform-backend-mongodb/refs/heads/main/scripts/tfbeadm && chmod +x ./tfbeadm
    MONGODB_URI=mongodb://<myserver>:27017/<mydb> ./tfbeadm create-indexes
    ```

    !!! warning

        `mongosh` or `Docker` must be available on the machine running the commands

### Upgrading from a version before 1.3.0

Version 1.3.0 renamed two fields, and `tfbeadm` migrates existing documents.
Both commands are safe to run more than once.

- A lock stores its ID in `lock_id` rather than in the document `_id`, so the same lock ID can be used on two states.
  A lock still held at the upgrade, a stale one from a crashed run included, cannot be released until `migrate-lock-id` copies its ID, so run it once the application is upgraded.
- State and history documents store `created_at` rather than `createdAt`.
  The application renames the field on every document it writes, and `migrate-created-at` renames it everywhere at once, for example before a query on `created_at`.

```bash
MONGODB_URI=mongodb://<myserver>:27017/<mydb> ./tfbeadm migrate-lock-id
MONGODB_URI=mongodb://<myserver>:27017/<mydb> ./tfbeadm migrate-created-at
```

## Configuration

Settings are read from `appsettings.json` and overridden by environment variables, with `__` as the section separator.

Setting | Environment variable | Default | Purpose
------- | --------------------- | ------- | -------
`DatabaseSettings:ConnectionString` | `DatabaseSettings__ConnectionString` | none | MongoDB connection string.
`DatabaseSettings:DatabaseName` | `DatabaseSettings__DatabaseName` | `tfbackend` | MongoDB database name.
`Features:IsHttpsRedirectionEnabled` | `Features__IsHttpsRedirectionEnabled` | `true` | Redirects HTTP requests to HTTPS, to disable when TLS terminates at an ingress.
`Features:IsScalarEnabled` | `Features__IsScalarEnabled` | `false` | Serves the OpenAPI definition and the Scalar web page on `/scalar`.
`Authentication:CredentialCacheSeconds` | `Authentication__CredentialCacheSeconds` | `60` | How long a verified credential skips BCrypt.
`Authentication:MaxFailedAttempts` | `Authentication__MaxFailedAttempts` | `10` | Consecutive failures, per username and source address, before that pair is refused.
`Authentication:LockoutSeconds` | `Authentication__LockoutSeconds` | `300` | How long a pair stays refused once locked out.
`Network:KnownProxies` | `Network__KnownProxies__0`, `__1`, ... | none | Reverse proxy addresses whose `X-Forwarded-For` header the application believes.
`Network:KnownNetworks` | `Network__KnownNetworks__0`, `__1`, ... | none | Same, as CIDR ranges.
`Network:TrustAllProxies` | `Network__TrustAllProxies` | `false` | Believes the forwarded headers of any caller, safe only where the application is reachable through the ingress alone.

!!! warning

    Behind a reverse proxy, at least one of `Network:KnownProxies`, `Network:KnownNetworks` or `Network:TrustAllProxies` must be set.
    Left unset, the application sees the proxy's own address on every request: every caller then shares a single lockout bucket, and every authentication-failure log entry names the proxy instead of the attacker.

The health check is served on `/health`.

!!! warning

    The application serves plain HTTP, and Terraform sends the password on every request, so TLS must terminate in front of it, at an ingress or a reverse proxy.
    `skip_cert_verification` in a backend block turns that protection off, and is for local tests only.

## Installation

### Kubernetes

Add the Helm chart repository:

```bash
helm repo add devpro https://devpro.github.io/helm-charts
helm repo update
```

Create a values.yaml file with the configuration, starting from the examples:

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
      openTelemetry:
        enabled: false
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

Install and manage the application with Helm, `<tag>` being one of the [published image tags](https://hub.docker.com/r/devprofr/terraform-backend-mongodb/tags), since the chart's default tag is not published:

```bash
helm upgrade --install tfbackend devpro/terraform-backend-mongodb -f values.yaml --set webapi.tag=<tag> \
  --create-namespace --namespace tfbackend
```
