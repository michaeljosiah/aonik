using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Catalog;

// ─── Spec 073 dependency reads (074/075/076) ────────────────────────────────
// The raw authoring reads the Admin UI page specs gate on: stored narrowing
// (null-vs-explicit preserved), the raw content block with server-computed
// staleness, the tenant content-status list, and the size plan by product id
// (status-agnostic — the PUBLIC box-plan read is Active-only, so a draft
// bundle's existing plan must still be visible to its author).

/// <summary>GET /commerce/admin/products/{productId}/option-groups — Spec 074.</summary>
public class GetProductNarrowingEndpoint : EndpointWithoutRequest<IReadOnlyList<ProductNarrowingLineDto>>
{
    private readonly IProductOptionService _options;

    public GetProductNarrowingEndpoint(IProductOptionService options) => _options = options;

    public override void Configure()
    {
        Get("/commerce/admin/products/{productId:guid}/option-groups");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary = "The product's STORED option narrowing (raw lines, null allowed-keys preserved).");
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await _options.GetNarrowingAsync(Route<Guid>("productId"), ct), ct);
}

/// <summary>GET /commerce/admin/products/{productId}/content — Spec 075.</summary>
public class GetAdminProductContentEndpoint : EndpointWithoutRequest<AdminProductContentDto>
{
    private readonly IProductContentService _content;

    public GetAdminProductContentEndpoint(IProductContentService content) => _content = content;

    public override void Configure()
    {
        Get("/commerce/admin/products/{productId:guid}/content");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary = "The RAW content block + variants + server-computed staleness (never the public resolution).");
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await _content.GetAdminAsync(Route<Guid>("productId"), ct), ct);
}

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

/// <summary>GET /commerce/admin/products/{productId}/size-plan — Spec 076: the
/// admin plan read by PRODUCT ID, independent of product status, so a draft
/// bundle's hidden plan can never be mistaken for a missing one.</summary>
public class GetAdminSizePlanEndpoint : EndpointWithoutRequest<BoxPlanDto>
{
    private readonly IBundleSizePlanService _plans;

    public GetAdminSizePlanEndpoint(IBundleSizePlanService plans) => _plans = plans;

    public override void Configure()
    {
        Get("/commerce/admin/products/{productId:guid}/size-plan");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary = "The bundle's size plan by product id (any product status). 404 = no plan authored.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var plan = await _plans.GetForProductAsync(Route<Guid>("productId"), ct);
        if (plan is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        await Send.OkAsync(plan, ct);
    }
}
