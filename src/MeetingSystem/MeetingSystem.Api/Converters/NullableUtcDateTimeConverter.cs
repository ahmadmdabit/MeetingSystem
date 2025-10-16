using System.Text.Json;
using System.Text.Json.Serialization;

namespace MeetingSystem.Api.Converters;

/// <summary>
/// A custom JsonConverter to ensure all nullable DateTime objects are serialized
/// to UTC format with the 'Z' suffix, or as null.
/// </summary>
public class NullableUtcDateTimeConverter : JsonConverter<DateTime?>
{
    /// <inheritdoc />
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }
        return reader.GetDateTime().ToUniversalTime();
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            // Ensure the DateTime kind is UTC before serializing.
            var utcValue = DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);
            // Use the "O" (round-trip) format specifier, which includes the 'Z'.
            writer.WriteStringValue(utcValue.ToString("o"));
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}