// creates the builder
var builder = WebApplication.CreateBuilder(args);
var configuration = new ApplicationConfiguration(builder.Configuration);

// adds services to the container
builder.Services.AddControllers(x => x.InputFormatters.Insert(0, new RawRequestBodyFormatter()));
builder.Services.AddInfrastructure(configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGenWithBasicAuth(configuration);
builder.Services.AddBasicAuthentication();
builder.Services.AddHealthChecks();
builder.Services.AddInvalidModelStateLog();

// create the application and configures the HTTP request pipeline
var app = builder.Build();
app.UseSwagger(configuration);
app.UseHttpsRedirection(configuration);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks(WebApiConfiguration.HealthCheckEndpoint).AllowAnonymous();

// runs the application
app.Run();

// adds explicit class definition for integration test project
public partial class Program { }
