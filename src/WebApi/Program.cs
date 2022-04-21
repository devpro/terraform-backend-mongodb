var builder = WebApplication.CreateBuilder(args);

var configuration = new ApplicationConfiguration(builder.Configuration);

// adds services to the container.
builder.Services.AddControllers();
builder.Services.AddInfrastructure();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// configures the HTTP request pipeline
app.UseSwagger(configuration);
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
