using Aonik.Platform.Contracts.Models.Compliance;
using Aonik.Platform.Contracts.Services.Compliance;
using Aonik.SharedKernel.Abstractions;
using FastEndpoints;

namespace Aonik.Platform.Endpoints.Admin.Compliance;

internal sealed record ListAuditLogsEndpointRequest
{
    [QueryParam]
    public int PageNumber { get; init; } = 1;

    [QueryParam]
    public int PageSize { get; init; } = 20;

    [QueryParam]
    public string? Search { get; init; }

    [QueryParam]
    public string? Action { get; init; }

    [QueryParam]
    public string? ResourceType { get; init; }

    [QueryParam]
    public Guid? ResourceId { get; init; }

    [QueryParam]
    public string? CorrelationId { get; init; }
}

internal sealed class ListAuditLogsEndpoint : Endpoint<ListAuditLogsEndpointRequest, PagedResult<AuditLogListItem>>
{
    private readonly IAuditLogAdminService _auditLogAdminService;

    public ListAuditLogsEndpoint(IAuditLogAdminService auditLogAdminService)
    {
        _auditLogAdminService = auditLogAdminService;
    }

    public override void Configure()
    {
        Get("/admin/audit-logs");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(ListAuditLogsEndpointRequest req, CancellationToken ct)
    {
        var result = await _auditLogAdminService.ListAuditLogsAsync(
            new ListAuditLogsRequest(
                req.PageNumber,
                req.PageSize,
                req.Search,
                req.Action,
                req.ResourceType,
                req.ResourceId,
                req.CorrelationId),
            ct);

        await Send.OkAsync(result, ct);
    }
}
