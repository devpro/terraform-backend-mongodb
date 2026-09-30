using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using AwesomeAssertions;
using Devpro.TerraformBackend.WebApi.IntegrationTests.Hosting;
using Xunit;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests.Resources;

/// <summary>
/// What comes back out of storage must be what went in.
/// <para>
/// The suite exercised the storage layer before this, including a real <c>terraform apply</c>, but nothing
/// ever compared the state that was read against the state that was written, and the sample only ever
/// produced strings and small integers. The values that break could not appear, which is how a 500 on one
/// class of number and a silent reshaping of another went unnoticed.
/// </para>
/// <para>
/// Equality is asserted semantically rather than byte for byte. The state is stored as a queryable BSON
/// document, which is the point of the project, so <c>1e3</c> legitimately reads back as <c>1000.0</c> and
/// <c>-0.0</c> as <c>0.0</c>. Terraform re-parses the JSON it receives, so those are equal for every purpose
/// that matters. What must never happen is a scalar changing into an object, a value being lost, or the write
/// failing outright.
/// </para>
/// </summary>
[Trait("Category", "IntegrationTests")]
public class StateFidelityTest(TestWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    [Theory]
    // the shapes a real Terraform state is made of
    [InlineData("""{"version":4,"terraform_version":"1.9.0","serial":1,"lineage":"abc","outputs":{},"resources":[]}""")]
    [InlineData("""{"resources":[{"mode":"managed","type":"local_file","instances":[{"attributes":{"content":"x","id":"a1b2"}}]}]}""")]
    // keys that MongoDB gives special meaning to, confirmed to round-trip on 8.2
    [InlineData("""{"tags":{"kubernetes.io/cluster":"x","$ref":"y"}}""")]
    // ordinary numbers across the boundaries between BSON numeric types
    [InlineData("""{"a":0,"b":-1,"c":2147483647,"d":2147483648,"e":9223372036854775807}""")]
    [InlineData("""{"a":1.5,"b":-2.25,"c":0.1}""")]
    // an integer beyond Int64, which used to fail the write with a 500
    [InlineData("""{"v":123456789012345678901234567890}""")]
    // a magnitude beyond Double, which used to be stored as Infinity and read back as an object
    [InlineData("""{"v":1e400}""")]
    [InlineData("""{"v":-1e400}""")]
    // the remaining JSON value classes
    [InlineData("""{"t":true,"f":false,"n":null,"s":"","arr":[1,"two",null,{"deep":[]}]}""")]
    public async Task State_ReadsBackEqualToWhatWasWritten(string payload)
    {
        // Arrange
        var name = UniqueStateName();
        TrackState(TestCredentials.Tenant, name);
        var client = CreateClient(true);

        // Act
        var createResponse = await client.PostAsync($"/{TestCredentials.Tenant}/state/{name}",
            new StringContent(payload, System.Text.Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "storing a valid Terraform state must not fail");

        var read = await client.GetStringAsync($"/{TestCredentials.Tenant}/state/{name}",
            TestContext.Current.CancellationToken);

        // Assert
        JsonNode.DeepEquals(JsonNode.Parse(read), JsonNode.Parse(payload))
            .Should().BeTrue("the state read back was {0}", read);
    }

    [Fact]
    public async Task State_AboveTheDocumentLimit_IsRefusedWithPayloadTooLarge()
    {
        // Arrange: a state comfortably beyond the 16 MB BSON document limit, which is a permanent property of
        // storing the state as a queryable document and therefore has to be reported rather than escaped
        var name = UniqueStateName();
        TrackState(TestCredentials.Tenant, name);
        var client = CreateClient(true);

        var resources = new JsonArray();
        for (var index = 0; index < 20000; index++)
        {
            resources.Add(new JsonObject { ["name"] = $"r{index}", ["blob"] = new string('x', 900) });
        }
        var oversized = new JsonObject { ["version"] = 4, ["serial"] = 1, ["resources"] = resources }.ToJsonString();

        // Act
        var response = await client.PostAsync($"/{TestCredentials.Tenant}/state/{name}",
            new StringContent(oversized, System.Text.Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);

        // Assert: the failure lands in the middle of a terraform apply, after the lock is taken and after the
        // real infrastructure has changed, so it has to say what happened rather than surface as a 500
        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("16", "the message must name the limit that was exceeded");
    }

    [Fact]
    public async Task State_WithMalformedJson_IsRefusedWithBadRequest()
    {
        // Arrange: malformed input must stay a 400 and must not be confused with the size failure above
        var name = UniqueStateName();
        TrackState(TestCredentials.Tenant, name);
        var client = CreateClient(true);

        // Act
        var response = await client.PostAsync($"/{TestCredentials.Tenant}/state/{name}",
            new StringContent("""{"version": }""", System.Text.Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
