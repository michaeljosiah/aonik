using System;

namespace Aonik.Application.Abstractions;

/// <summary>
/// Defines an interface for JSON serialization operations.
/// </summary>
public interface IJsonSerializer
{
    /// <summary>
    /// Serializes an object to a JSON string.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="obj">The object to serialize.</param>
    /// <returns>The JSON string representation of the object.</returns>
    string Serialize<T>(T obj);

    /// <summary>
    /// Serializes an object to a JSON string.
    /// </summary>
    /// <param name="obj">The object to serialize.</param>
    /// <param name="type">The type of the object.</param>
    /// <returns>The JSON string representation of the object.</returns>
    string Serialize(object obj, Type type);

    /// <summary>
    /// Deserializes a JSON string to an object of the specified type.
    /// </summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>The deserialized object.</returns>
    T? Deserialize<T>(string json);

    /// <summary>
    /// Deserializes a JSON string to an object of the specified type.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <param name="type">The target type.</param>
    /// <returns>The deserialized object.</returns>
    object? Deserialize(string json, Type type);
}
