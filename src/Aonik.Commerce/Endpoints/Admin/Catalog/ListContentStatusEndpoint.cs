using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Catalog;

/// <summary>GET /commerce/admin/content — Spec 075's rail/KPI/queue flags.</summary>
public class ListContentStatusEndpoint : EndpointWithoutRequest<PagedResult<ContentStatusRowDto>>
{
    private readonly IProductContentService _content;

    public ListContentStatusEndpoint(IProductContentService content) => _content = content;

    public override void Configure()
    {
        Get("/commerce/admin/content");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary = "Paged per-product content flags (block-exists, requiresReview, server-computed isStale).");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var page = Query<int?>("page", isRequired: false) ?? 1;
        var pageSize = Query<int?>("pageSize", isRequired: false) ?? 50;
        await Send.OkAsync(await _content.ListAdminStatusAsync(page, pageSize, ct), ct);
    }
}
