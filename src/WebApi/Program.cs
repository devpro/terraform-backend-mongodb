// creates the web application builder and adds services to the container
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers(x => x.InputFormatters.Insert(0, new RawRequestBodyFormatter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddAuthentication()
    .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>(BasicAuthenticationClient.AuthenticationScheme, null);
builder.Services.AddHealthChecks();
builder.Services.AddInvalidModelStateLog();

// reads the application configuration and configures additional services
var configuration = new ApplicationConfiguration(builder.Configuration);
builder.Services.AddMongoDbInfrastructure(configuration);
builder.Services.AddOpenApiWithBasicAuth(configuration);

// creates the application and configures the HTTP request pipeline
var app = builder.Build();

if (configuration.IsScalarEnabled)
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle(configuration.OpenApiInfo.Title ?? "Terraform MongoDB Backend API")
            .WithTheme(ScalarTheme.Kepler)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
        options.AddPreferredSecuritySchemes("basic");
    });
}

if (configuration.IsHttpsRedirectionEnabled)
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks(ApplicationConfiguration.HealthCheckEndpoint)
    .AllowAnonymous();

// runs the application
app.Run();
