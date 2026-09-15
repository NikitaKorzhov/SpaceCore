using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpaceCore.Common;

public class DdMmYyyyDateTimeConverter : JsonConverter<DateTime>
{
    public const string Format = "dd-MM-yyyy HH:mm";

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();

        if (string.IsNullOrEmpty(value))
        {
            throw new JsonException($"Date value cannot be null or empty. Expected format: '{Format}'.");
        }

        if (!DateTime.TryParseExact(value, Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
        {
            throw new JsonException($"Could not parse '{value}' as a date. Expected format: '{Format}'.");
        }

        return result;
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(Format, CultureInfo.InvariantCulture));
    }
}
