using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShiftPlatform.Services;

/// <summary>
/// Accepts time values as "HH:mm" (what an HTML &lt;input type="time"&gt; sends)
/// as well as "HH:mm:ss" / round-trip formats. Serialises as "HH:mm:ss".
/// </summary>
public class FlexibleTimeOnlyJsonConverter : JsonConverter<TimeOnly>
{
    private static readonly string[] Formats = { "HH:mm", "HH:mm:ss", "HH:mm:ss.FFFFFFF" };

    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (string.IsNullOrEmpty(value))
            throw new JsonException("Expected a time value.");
        if (TimeOnly.TryParseExact(value, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
            return exact;
        if (TimeOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return parsed;
        throw new JsonException($"Could not parse '{value}' as a time.");
    }

    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
}
