using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hostpad.Core.Storage;

/// <summary>
/// One serializer configuration for everything Hostpad writes, so the vault and
/// the settings file cannot drift apart in casing or enum handling.
/// </summary>
public static class HostpadJson
{
    /// <summary>Enums are written by name: a reordered enum must not silently change stored data.</summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Compact form for the vault payload, which no human reads.</summary>
    public static readonly JsonSerializerOptions PayloadOptions = new(Options)
    {
        WriteIndented = false,
    };

    public static byte[] SerializeToUtf8Bytes<T>(T value, JsonSerializerOptions options) =>
        JsonSerializer.SerializeToUtf8Bytes(value, options);

    public static T Deserialize<T>(ReadOnlySpan<byte> utf8Json, JsonSerializerOptions options)
    {
        return JsonSerializer.Deserialize<T>(utf8Json, options)
               ?? throw new InvalidDataException($"The stored JSON deserialized to null ({typeof(T).Name}).");
    }
}
