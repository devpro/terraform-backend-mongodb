using AwesomeAssertions;
using Devpro.TerraformBackend.Infrastructure.MongoDb.Serialization;
using MongoDB.Bson;
using Xunit;

namespace Devpro.TerraformBackend.WebApi.UnitTests.Serialization;

/// <summary>
/// The other pure half of H2: rendering a stored BSON value back as plain JSON.
/// <para>
/// This is what replaced <c>BsonValue.ToJson()</c>, which emitted MongoDB Extended JSON and turned a non-finite <c>Double</c> or a <c>Decimal128</c> into a <c>$</c>-prefixed object instead of a number.
/// These tests check the renderer directly, without a stored document or an HTTP response to read it back through.
/// </para>
/// </summary>
[Trait("Category", "UnitTests")]
public class BsonToJsonConverterTest
{
    [Theory]
    [InlineData(1000.0, "1000.0")] // a whole-numbered double must stay distinguishable from an integer
    [InlineData(-2.25, "-2.25")]
    [InlineData(0.1, "0.1")] // the shortest form that reads back as the same double, not seventeen digits
    public void Convert_WithADouble_WritesItAsPlainJson(double value, string expected)
    {
        BsonToJsonConverter.Convert(new BsonDouble(value)).Should().Be(expected);
    }

    [Fact]
    public void Convert_WithADecimal128_WritesItAsAPlainNumber()
    {
        var json = BsonToJsonConverter.Convert(new BsonDecimal128(Decimal128.Parse("123456789012345678901234567890")));

        json.Should().Be("123456789012345678901234567890");
    }

    [Fact]
    public void Convert_WithAPositiveInfinity_WritesAMagnitudeNoDoubleCanReach()
    {
        // Arrange: JsonToBsonConverter never stores an infinity, so only a document written by another client holds one.
        // Handled anyway, since the alternative is emitting an object or a null in place of a number the caller once sent.
        var json = BsonToJsonConverter.Convert(new BsonDouble(double.PositiveInfinity));

        json.Should().Be("1e999");
    }

    [Fact]
    public void Convert_WithANegativeInfinity_WritesANegativeMagnitudeNoDoubleCanReach()
    {
        var json = BsonToJsonConverter.Convert(new BsonDouble(double.NegativeInfinity));

        json.Should().Be("-1e999");
    }

    [Fact]
    public void Convert_WithAnInt32_WritesItAsPlainJson()
    {
        BsonToJsonConverter.Convert(new BsonInt32(42)).Should().Be("42");
    }

    [Fact]
    public void Convert_WithAnInt64_WritesItAsPlainJson()
    {
        BsonToJsonConverter.Convert(new BsonInt64(9223372036854775807)).Should().Be("9223372036854775807");
    }

    [Fact]
    public void Convert_WithAString_EscapesAndQuotesIt()
    {
        BsonToJsonConverter.Convert(new BsonString("a\"b")).Should().Be("\"a\\\"b\"");
    }

    [Fact]
    public void Convert_WithADocument_PreservesFieldOrderAndNesting()
    {
        var document = new BsonDocument
        {
            ["b"] = 2,
            ["a"] = new BsonDocument { ["nested"] = true }
        };

        BsonToJsonConverter.Convert(document).Should().Be("""{"b":2,"a":{"nested":true}}""");
    }

    [Fact]
    public void Convert_WithAnArray_PreservesOrder()
    {
        var array = new BsonArray { 1, "two", BsonNull.Value };

        BsonToJsonConverter.Convert(array).Should().Be("""[1,"two",null]""");
    }
}
