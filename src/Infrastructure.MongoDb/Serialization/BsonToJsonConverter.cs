using System;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using MongoDB.Bson;

namespace Devpro.TerraformBackend.Infrastructure.MongoDb.Serialization;

/// <summary>
/// Renders a stored state as the plain JSON a Terraform client expects.
/// <para>
/// <c>BsonValue.ToJson</c> is not used because it emits MongoDB Extended JSON,
/// where a non-finite <c>Double</c> and a <c>Decimal128</c> come out as <c>$</c>-prefixed objects in every output mode, so a scalar would reach Terraform as an object.
/// Only the output changes: the document in <c>tf_state</c> stays queryable field by field.
/// </para>
/// </summary>
public static class BsonToJsonConverter
{
    /// <summary>
    /// A magnitude no double can reach, written in place of an infinity, which JSON cannot express.
    /// <see cref="JsonToBsonConverter"/> never stores one, but a document written by another client can hold it, and a number is closer to what was sent than an object or a null.
    /// </summary>
    private const string PositiveOverflowLiteral = "1e999";

    public static string Convert(BsonValue value)
    {
        var builder = new StringBuilder();
        Write(builder, value);
        return builder.ToString();
    }

    private static void Write(StringBuilder builder, BsonValue value)
    {
        switch (value.BsonType)
        {
            case BsonType.Document:
                WriteDocument(builder, value.AsBsonDocument);
                break;
            case BsonType.Array:
                WriteArray(builder, value.AsBsonArray);
                break;
            case BsonType.String:
                WriteString(builder, value.AsString);
                break;
            case BsonType.Boolean:
                builder.Append(value.AsBoolean ? "true" : "false");
                break;
            case BsonType.Null:
                builder.Append("null");
                break;
            case BsonType.Int32:
                builder.Append(value.AsInt32.ToString(CultureInfo.InvariantCulture));
                break;
            case BsonType.Int64:
                builder.Append(value.AsInt64.ToString(CultureInfo.InvariantCulture));
                break;
            case BsonType.Double:
                WriteDouble(builder, value.AsDouble);
                break;
            case BsonType.Decimal128:
                builder.Append(value.AsDecimal128.ToString());
                break;
            default:
                // every value in tf_state came from the JSON a client sent, so nothing else should occur;
                // falling back keeps an unexpected document readable rather than failing the request
                WriteString(builder, value.ToString() ?? string.Empty);
                break;
        }
    }

    private static void WriteDocument(StringBuilder builder, BsonDocument document)
    {
        builder.Append('{');
        var first = true;
        foreach (var element in document)
        {
            if (!first) builder.Append(',');
            first = false;
            WriteString(builder, element.Name);
            builder.Append(':');
            Write(builder, element.Value);
        }

        builder.Append('}');
    }

    private static void WriteArray(StringBuilder builder, BsonArray array)
    {
        builder.Append('[');
        for (var index = 0; index < array.Count; index++)
        {
            if (index > 0) builder.Append(',');
            Write(builder, array[index]);
        }

        builder.Append(']');
    }

    /// <summary>
    /// Writes the shortest representation that reads back as the same double, so that <c>0.1</c> is returned as <c>0.1</c> rather than the driver's <c>0.10000000000000001</c>.
    /// </summary>
    private static void WriteDouble(StringBuilder builder, double value)
    {
        if (double.IsNaN(value))
        {
            builder.Append("null");
            return;
        }

        if (double.IsInfinity(value))
        {
            builder.Append(double.IsPositiveInfinity(value) ? PositiveOverflowLiteral : "-" + PositiveOverflowLiteral);
            return;
        }

        var text = value.ToString("R", CultureInfo.InvariantCulture);
        builder.Append(text);

        // a whole-numbered double must keep a marker of being fractional, or it reads back as an integer
        if (text.IndexOfAny(['.', 'e', 'E']) < 0)
        {
            builder.Append(".0");
        }
    }

    private static void WriteString(StringBuilder builder, string value)
    {
        builder.Append('"');
        builder.Append(JavaScriptEncoder.UnsafeRelaxedJsonEscaping.Encode(value));
        builder.Append('"');
    }
}
