// creates the web application builder
var builder = WebApplication.CreateBuilder(args);

// adds services to the container
builder.Services.AddControllers(x => x.InputFormatters.Insert(0, new RawRequestBodyFormatter()));
builder.Services.AddOpenApi();
builder.Services.AddAuthentication()
    .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>(BasicAuthenticationClient.AuthenticationScheme, null);
builder.Services.AddHealthChecks()
    .AddCheck<MongoDbHealthCheck>("mongodb");
builder.Services.AddInvalidModelStateLog();

// reads the application configuration and configures additional services
var configuration = new ApplicationConfiguration(builder.Configuration);
builder.Services.AddSingleton(configuration);
builder.Services.AddMongoDbInfrastructure(configuration);
builder.Services.AddOpenApiWithBasicAuth(configuration);
builder.Services.AddCredentialAuthentication();
builder.Services.AddTrustedProxies(configuration);

// creates the application and configures the HTTP request pipeline
var app = builder.Build();

// must run before anything reads the caller's address, so that the lockout and the authentication failure
// log see the real client rather than the reverse proxy in front of the application
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

// runs the application
await app.RunAsync();
