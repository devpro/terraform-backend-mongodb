// creates the web application builder and adds services to the container
var builder = WebApplication.CreateBuilder(args);
var configuration = new ApplicationConfiguration(builder.Configuration);
builder.Services.AddControllers(x => x.InputFormatters.Insert(0, new RawRequestBodyFormatter()));
builder.Services.AddMongoDbInfrastructure(configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGenWithBasicAuth(configuration);
builder.Services.AddAuthentication()
    .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>(BasicAuthenticationClient.AuthenticationScheme, null);
builder.Services.AddHealthChecks();
builder.Services.AddInvalidModelStateLog();

// creates the application and configures the HTTP request pipeline
var app = builder.Build();
app.UseSwagger(configuration);
if (configuration.IsHttpsRedirectionEnabled)
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks(ApplicationConfiguration.HealthCheckEndpoint).AllowAnonymous();

// runs the application
app.Run();
