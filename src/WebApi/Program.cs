var builder = WebApplication.CreateBuilder(args);

var configuration = new ApplicationConfiguration(builder.Configuration);

// adds services to the container.
builder.Services.AddControllers(x => x.InputFormatters.Insert(0, new RawRequestBodyFormatter()));
builder.Services.AddInfrastructure(configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddBehaviors();

var app = builder.Build();

// configures the HTTP request pipeline
app.UseSwagger(configuration);
app.UseHttpsRedirection(configuration);
app.UseAuthorization();
app.MapControllers();

app.Run();
