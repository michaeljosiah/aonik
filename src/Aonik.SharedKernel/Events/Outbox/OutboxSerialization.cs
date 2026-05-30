using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aonik.SharedKernel.Events.Outbox;

/// <summary>
/// Canonical JSON options for integration-event payloads written to and read from
/// the outbox. These must stay stable: a row persisted by one process is
/// deserialized by another, so changing these options is a wire-format change.
/// </summary>
public static class OutboxSerialization
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
