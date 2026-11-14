using System.Text.Json;

using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Agents.Services.Connectors;

internal sealed record ConnectorMutationJournal(int SchemaVersion, string Status, Guid ConnectionId, string ProviderAccountId,
    ConnectorActionRef Action, ConnectorUseContext Context, string ArgumentsHash, DateTime StartedAtUtc,
    Guid LeaseId, DateTime LeaseExpiresAtUtc, JsonElement? Result = null, string? ErrorCode = null, DateTime? CompletedAtUtc = null);
