using Devpro.TerraformBackend.WebApi.IntegrationTests.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests.Behaviors;

[Trait("Category", "IntegrationTests")]
public class InvalidModelStateBehaviorTest(WebApplicationFactory<Program> factory)
    : IntegrationTestBase(factory)
{
    [Fact]
    [Trait("Mode", "Readonly")]
    public async Task InvalidModelStateBehavior_OnInvalidModelState_LogsWarning()
    {
        // Arrange
        var loggerMock = new Mock<ILogger>();
        var loggerFactoryMock = new Mock<ILoggerFactory>();
        loggerFactoryMock.Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(loggerMock.Object);
        var client = CreateClient(isAuthorizationNeeded: true, builderConfiguration: builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(loggerFactoryMock.Object);
            });
        });
        var invalidModel = new { Name = (string?)null };
        var name = Faker.Random.Word();

        // Act
        var response = await client.PostAsync($"/dummy/state/{name}/lock", Serialize(invalidModel));

        // Assert
        await response.CheckResponseAndGetContent(System.Net.HttpStatusCode.BadRequest, "application/json; charset=utf-8",
            "{\"type\":\"https:\\/\\/tools\\.ietf\\.org\\/html\\/rfc9110#section-15\\.5\\.1\",\"title\":\"One or more validation errors occurred\\.\",\"status\":400,\"errors\":{\"Id\":\\[\"The Id field is required\\.\"\\],\"name\":\\[\"The Name field is required\\.\"\\]},\"traceId\":\".*\"}",
            isRegexMatch: true);
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains($"Invalid model state for /dummy/state/{name}/lock")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once());
    }
}
