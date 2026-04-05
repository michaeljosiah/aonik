using Aonik.SharedKernel.Abstractions;

namespace Aonik.Platform.Contracts.Models.Compliance;

public record ListAuditLogsRequest(
    int PageNumber = 1,
    int PageSize = 20,
    string? Search = null,
    string? Action = null,
    string? ResourceType = null,
    Guid? ResourceId = null,
    string? CorrelationId = null);

public record AuditLogListItem(
    Guid Id,
    Guid TenantId,
    DateTime Timestamp,
    string ActorType,
    Guid ActorId,
    string Action,
    string ResourceType,
    Guid ResourceId,
    string DetailsJson,
    string CorrelationId);
