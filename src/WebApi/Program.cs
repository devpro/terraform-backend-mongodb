var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddAuthentication()
    .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>(BasicAuthenticationClient.AuthenticationScheme, null);
builder.Services.AddHealthChecks()
    .AddCheck<MongoDbHealthCheck>("mongodb", timeout: ApplicationConfiguration.HealthCheckTimeout);
builder.Services.AddInvalidModelStateLog();

var configuration = new ApplicationConfiguration(builder.Configuration);
builder.Services.AddSingleton(configuration);
builder.Services.AddMongoDbInfrastructure(configuration);
builder.Services.AddOpenApiWithBasicAuth(configuration);
builder.Services.AddCredentialAuthentication(configuration);
builder.Services.AddTrustedProxies(configuration);

var app = builder.Build();

// first, so that the lockout and the failure log see the client rather than the reverse proxy
app.UseForwardedHeaders();

if (configuration.IsScalarEnabled)
{
    app.MapOpenApi()
        .AllowAnonymous();
    app.MapScalarApiReference(options =>
        {
            options
                .WithTitle(configuration.OpenApiInfo.Title ?? "Terraform MongoDB Backend API")
                .WithTheme(ScalarTheme.Kepler)
                .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
            options.AddPreferredSecuritySchemes("basic");
        })
        .AllowAnonymous();
}

if (configuration.IsHttpsRedirectionEnabled)
{
    app.UseHttpsRedirection();
}

app.MapControllers();
app.MapHealthChecks(ApplicationConfiguration.HealthCheckEndpoint)
    .AllowAnonymous();

await app.RunAsync();
