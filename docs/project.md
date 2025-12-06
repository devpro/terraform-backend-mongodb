# Project

## Design

### Programming languages

The code is mainly written in C# / .NET: open-source, cross-platform (Linux/Windows), highly performant, object-oriented.

## Automation

### CI/CD pipelines

GitHub Actions are triggered to automate the integration and delivery of the application:

Name      | Role                     | Definition file
----------|--------------------------|----------------------------
**CI**    | Continuous Integration   | `.github/workflows/ci.yaml`
**PKG**   | Continuous Delivery      | `.github/workflows/pkg.yaml`
**Pages** | Continuous Documentation | `.github/workflows/pages.yaml`

GitHub Variables are defined (in **General** / **Security** / **Secrets and Variables** / **Actions**):

- DOCKERHUB_TOKEN
- DOCKERHUB_USERNAME
- SONAR_HOST_URL
- SONAR_ORG
- SONAR_PROJECT_KEY
- SONAR_TOKEN

## Backlog

### V2.0 (coming in 2026)

Feature requests:

- :material-square: Store in the database only one version of the state in tf_state (the latest) and save the others in tf_state_revision
- :material-square: Send traces, logs, metrics to OpenTelemetry Collector
