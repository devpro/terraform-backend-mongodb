var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

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

// a state holds every secret of its workspace in the clear, so no response is kept by a cache between Terraform and the backend
app.Use((context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers.XContentTypeOptions = "nosniff";
    return next(context);
});

// called explicitly, since the framework otherwise inserts both ahead of everything in this file,
// and the credential would be checked, and its failure counted, before the forwarded address is applied
app.UseAuthentication();
app.UseAuthorization();

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
