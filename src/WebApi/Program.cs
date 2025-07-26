// creates the web application builder and adds services to the container
var builder = WebApplication.CreateBuilder(args);
var configuration = new ApplicationConfiguration(builder.Configuration);
builder.Services.AddControllers(x => x.InputFormatters.Insert(0, new RawRequestBodyFormatter()));
builder.Services.AddInfrastructure(configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGenWithBasicAuth(configuration);
builder.Services.AddBasicAuthentication();
builder.Services.AddHealthChecks();
builder.Services.AddInvalidModelStateLog();

// creates the application and configures the HTTP request pipeline
var app = builder.Build();
app.UseSwagger(configuration);
app.UseHttpsRedirection(configuration);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks(WebApiConfiguration.HealthCheckEndpoint).AllowAnonymous();

// runs the application
app.Run();

[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "S1118:Utility classes should not have public constructors", Justification = "Explicit class definition needed for integration test project")]
public partial class Program { }
