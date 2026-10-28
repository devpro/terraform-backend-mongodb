# Project

## Design

The application is written in C# / .NET: open-source, cross-platform (Linux/Windows), highly performant, object-oriented.

## Automation

Application lifecycle management is done through GitHub.

GitHub Actions are triggered to automate the integration and delivery of the application.

Name      | Role                     | Actions
----------|--------------------------|-----------------------------------------------------
**CI**    | Continuous Integration   | Checks the quality of the code and any vulnerability
**PKG**   | Continuous Delivery      | Build the artifacts (container image)
**Pages** | Continuous Documentation | Build and deploy the documentation

## Backlog

### V2.0 (coming in 2026)

Feature requests:

- :material-square: Send traces, logs, metrics to OpenTelemetry Collector
