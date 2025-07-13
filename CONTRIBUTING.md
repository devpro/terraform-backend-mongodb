# Project development guide

## Solution design

This is a .NET 8 / C# codebase (open-source, cross-platform, free, object-oriented technologies).

### Projects

Project name             | Technology | Project type
------------------------ | ---------- | --------------------------
`Common.AspNetCore`      | .NET 8     | Library
`Common.MongoDb`         | .NET 8     | Library
`Common.Runtime`         | .NET 8     | Library
`Domain`                 | .NET 8     | Library
`Infrastructure.MongoDb` | .NET 8     | Library
`WebApi`                 | ASP.NET 8  | Web application (REST API)

### Packages (NuGet)

Name                     | Description
------------------------ | ----------------------------
`MongoDB.Bson`           | MongoDB BSON
`MongoDB.Driver`         | MongoDB .NET Driver
`Swashbuckle.AspNetCore` | OpenAPI / Swagger generation
`System.Text.Json`       | JSON support

## CI/CD pipelines

GitHub Actions are triggered to automate the application lifecycle:

- [CI](.github/workflows/ci.yaml) (Continuous Integration)
- [PKG](.github/workflows/pkg.yaml) (Continuous Delivery)

GitHub project has been configured, in General / Security / Secrets and Variables / Actions:

- DOCKERHUB_TOKEN
- DOCKERHUB_USERNAME
- SONAR_HOST_URL
- SONAR_ORG
- SONAR_PROJECT_KEY
- SONAR_TOKEN
