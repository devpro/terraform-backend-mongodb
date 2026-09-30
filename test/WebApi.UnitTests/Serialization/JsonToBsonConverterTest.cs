using System.Text.Json;
using AwesomeAssertions;
using Devpro.TerraformBackend.Infrastructure.MongoDb.Serialization;
using MongoDB.Bson;
using Xunit;

namespace Devpro.TerraformBackend.WebApi.UnitTests.Serialization;

/// <summary>
/// The pure half of H1 and H2: what BSON numeric type a JSON number literal becomes.
/// <para>
/// The controller and MongoDB are both out of scope here on purpose.
/// What is under test is the mapping in isolation, so a change to it is caught at the type level rather than only through a full HTTP round trip against a real database.
/// </para>
/// </summary>
[Trait("Category", "UnitTests")]
public class JsonToBsonConverterTest
{
    [Theory]
    [InlineData("0", BsonType.Int32)]
    [InlineData("2147483647", BsonType.Int32)] // Int32.MaxValue
    [InlineData("2147483648", BsonType.Int64)] // one beyond Int32
    [InlineData("9223372036854775807", BsonType.Int64)] // Int64.MaxValue
    [InlineData("123456789012345678901234567890", BsonType.Decimal128)] // beyond Int64, used to throw
    [InlineData("1.5", BsonType.Double)]
    [InlineData("1e3", BsonType.Double)]
    [InlineData("1e400", BsonType.Decimal128)] // beyond Double, used to be stored as Infinity
    [InlineData("-1e400", BsonType.Decimal128)]
    public void Convert_ChoosesTheBsonNumericTypeFromTheLiteral(string number, BsonType expectedType)
    {
        // Act
        var document = JsonToBsonConverter.Convert($$"""{"v":{{number}}}""");

        // Assert
        document["v"].BsonType.Should().Be(expectedType);
    }

    [Fact]
    public void Convert_WithMalformedJson_ThrowsJsonException()
    {
        var act = () => JsonToBsonConverter.Convert("""{"version": }""");

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Convert_WithANonObjectRoot_ThrowsJsonException()
    {
        // Arrange: a Terraform state is always an object, and BsonDocument.Parse could not have represented anything else either
        var act = () => JsonToBsonConverter.Convert("[1,2,3]");

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Convert_WithANumberBeyondEveryBsonNumericRange_ThrowsJsonException()
    {
        // Arrange: Decimal128 gives out at roughly 1e6144
        var act = () => JsonToBsonConverter.Convert("""{"v":1e7000}""");

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Convert_PreservesEveryOtherJsonValueKind()
    {
        // Act
        var document = JsonToBsonConverter.Convert(
            """{"t":true,"f":false,"n":null,"s":"x","arr":[1,"two",null],"nested":{"a":1}}""");

        // Assert
        document["t"].Should().Be(BsonBoolean.True);
        document["f"].Should().Be(BsonBoolean.False);
        document["n"].Should().Be(BsonNull.Value);
        document["s"].Should().Be(new BsonString("x"));
        document["arr"].AsBsonArray.Should().HaveCount(3);
        document["nested"].AsBsonDocument["a"].Should().Be(new BsonInt32(1));
    }

    [Fact]
    public void Convert_WithAnIntegerBeyondDecimal128Precision_ThrowsNamingThePrecision()
    {
        // Arrange: L10, forty-one digits, within range but beyond the 34 significant digits Decimal128 holds
        var act = () => JsonToBsonConverter.Convert("""{"v":12345678901234567890123456789012345678901}""");

        // Act & Assert
        act.Should().Throw<JsonException>().WithMessage("*34 significant digits*");
    }
}
