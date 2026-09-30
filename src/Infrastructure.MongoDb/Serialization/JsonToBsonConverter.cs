using System;
using System.Globalization;
using System.Text.Json;
using MongoDB.Bson;

namespace Devpro.TerraformBackend.Infrastructure.MongoDb.Serialization;

/// <summary>
/// Turns the JSON a Terraform client sends into the BSON document that is stored.
/// <para>
/// <c>BsonDocument.Parse</c> is not used because it throws on any integer beyond <see cref="long"/>.
/// The mapping matches <c>BsonDocument.Parse</c> for every value in range, since the stored shape is a
/// contract other applications read, and differs only where <c>BsonDocument.Parse</c> would fail.
/// </para>
/// </summary>
public static class JsonToBsonConverter
{
    /// <summary>
    /// Converts a JSON object. Throws <see cref="JsonException"/> on malformed input, which is what keeps a
    /// bad request distinguishable from a state that is merely too large.
    /// </summary>
    public static BsonDocument Convert(string json)
    {
        using var document = JsonDocument.Parse(json);

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("A Terraform state must be a JSON object.");
        }

        return (BsonDocument)ConvertElement(document.RootElement);
    }

    private static BsonValue ConvertElement(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => ConvertObject(element),
        JsonValueKind.Array => ConvertArray(element),
        JsonValueKind.String => new BsonString(element.GetString() ?? string.Empty),
        JsonValueKind.Number => ConvertNumber(element),
        JsonValueKind.True => BsonBoolean.True,
        JsonValueKind.False => BsonBoolean.False,
        JsonValueKind.Null => BsonNull.Value,
        _ => throw new JsonException($"Unsupported JSON value of kind {element.ValueKind}.")
    };

    private static BsonDocument ConvertObject(JsonElement element)
    {
        var document = new BsonDocument();
        foreach (var property in element.EnumerateObject())
        {
            document[property.Name] = ConvertElement(property.Value);
        }

        return document;
    }

    private static BsonArray ConvertArray(JsonElement element)
    {
        var array = new BsonArray();
        foreach (var item in element.EnumerateArray())
        {
            array.Add(ConvertElement(item));
        }

        return array;
    }

    /// <summary>
    /// Chooses the BSON numeric type from the literal as it was written, not from its value.
    /// <para>
    /// An integer literal becomes <c>Int32</c>, then <c>Int64</c>, then <c>Decimal128</c>:
    /// a thirty-digit integer is a number Terraform is entitled to send, and BSON can hold it.
    /// A literal with a fraction or an exponent becomes <c>Double</c>, or <c>Decimal128</c> beyond the range
    /// of a double, which would otherwise be stored as an infinity rather than the value written.
    /// </para>
    /// </summary>
    private static BsonValue ConvertNumber(JsonElement element)
    {
        var raw = element.GetRawText();
        var isIntegerLiteral = raw.IndexOfAny(['.', 'e', 'E']) < 0;

        if (isIntegerLiteral)
        {
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var int32Value))
            {
                return new BsonInt32(int32Value);
            }

            if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var int64Value))
            {
                return new BsonInt64(int64Value);
            }
        }
        else if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue)
                 && double.IsFinite(doubleValue))
        {
            return new BsonDouble(doubleValue);
        }

        if (Decimal128.TryParse(raw, out var decimalValue))
        {
            return new BsonDecimal128(decimalValue);
        }

        throw new JsonException(
            $"The number {raw} cannot be stored: BSON holds at most 34 significant digits and magnitudes up to about 1e6144.");
    }
}
