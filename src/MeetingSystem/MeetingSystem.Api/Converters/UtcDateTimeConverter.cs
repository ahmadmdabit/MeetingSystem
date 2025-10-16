using System.Text.Json;
using System.Text.Json.Serialization;

namespace MeetingSystem.Api.Converters;

/// <summary>
/// A custom JsonConverter to ensure all DateTime objects are serialized
/// to UTC format with the 'Z' suffix.
/// </summary>
public class UtcDateTimeConverter : JsonConverter<DateTime>
{
    /// <inheritdoc />
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // This ensures that incoming date strings are correctly parsed into a UTC DateTime object.
        return reader.GetDateTime().ToUniversalTime();
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        // Ensure the DateTime kind is UTC before serializing.
        var utcValue = DateTime.SpecifyKind(value, DateTimeKind.Utc);
        // Use the "O" (round-trip) format specifier, which includes the 'Z'.
        writer.WriteStringValue(utcValue.ToString("o"));
    }
}