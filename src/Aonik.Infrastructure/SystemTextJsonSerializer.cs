using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aonik.Application.Abstractions;

namespace Aonik.Infrastructure;

/// <summary>
/// Default JSON serializer implementation using System.Text.Json.
/// </summary>
public class SystemTextJsonSerializer : IJsonSerializer
{
    private readonly JsonSerializerOptions _options;

    /// <summary>
    /// Creates a new instance of <see cref="SystemTextJsonSerializer"/>
    /// </summary>
    /// <param name="options">Optional JSON serializer options.</param>
    public SystemTextJsonSerializer(JsonSerializerOptions? options = null)
    {
        _options = options ?? new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    /// <inheritdoc />
    public string Serialize<T>(T obj)
    {
        return JsonSerializer.Serialize(obj, _options);
    }

    /// <inheritdoc />
    public string Serialize(object obj, Type type)
    {
        return JsonSerializer.Serialize(obj, type, _options);
    }

    /// <inheritdoc />
    public T? Deserialize<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, _options);
    }

    /// <inheritdoc />
    public object? Deserialize(string json, Type type)
    {
        return JsonSerializer.Deserialize(json, type, _options);
    }
}
