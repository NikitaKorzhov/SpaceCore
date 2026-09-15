using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpaceCore.Common;

// Applied via [JsonConverter] on individual DateTime properties in request/response DTOs so dates
// serialize as "dd-MM-yyyy HH:mm" instead of the default ISO 8601. Note this converter only kicks in
// for the JSON body: query-string values (e.g. SearchAvailableHallsQuery) bypass it entirely, so
// handlers that read dates from the query string parse them manually using the same Format constant
// (see SearchAvailableHallsQueryHandler.ParseDate) to keep both input paths consistent.
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
