using System.Text.Json;
using UltimateManager.Models.DynamicForms;

namespace UltimateManager.Services;

public sealed class DynamicFieldValueSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public T? Deserialize<T>(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? default
            : JsonSerializer.Deserialize<T>(value, Options);
    }

    public string Serialize(FieldType fieldType, object? value) => fieldType switch
    {
        FieldType.Address => Serialize(value as AddressValue),
        FieldType.Phone => Serialize(value as PhoneNumberValue),
        FieldType.GeoPoint => Serialize(value as GeoPointValue),
        FieldType.Comment => Serialize(value as CommentValue),
        FieldType.Document => Serialize(value as DocumentValue),
        _ => Serialize(value)
    };
}
