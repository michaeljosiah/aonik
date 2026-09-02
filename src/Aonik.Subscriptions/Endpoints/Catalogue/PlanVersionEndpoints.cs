using Aonik.Subscriptions.Contracts.Models;
using Aonik.Subscriptions.Contracts.Services;

using FastEndpoints;

namespace Aonik.Subscriptions.Endpoints.Catalogue;

/// <summary>
/// Start a new draft version of a plan (Spec 087 §6). Price and entitlements are authored on the
/// draft and frozen when it publishes, so an existing subscriber is never re-priced.
/// </summary>
public class CreateDraftVersionEndpoint : Endpoint<CreatePlanVersionRequest, PlanVersionResponse>
{
    private readonly ICatalogueService _catalogue;

    public CreateDraftVersionEndpoint(ICatalogueService catalogue) => _catalogue = catalogue;

    public override void Configure()
    {
        Post("/subscriptions/admin/plans/{planId:guid}/versions");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary = "Start a draft version. A plan may hold only one draft at a time.");
    }

    public override async Task HandleAsync(CreatePlanVersionRequest req, CancellationToken ct)
    {
        var result = await _catalogue.CreateDraftVersionAsync(Route<Guid>("planId"), req, ct);
        await Send.CreatedAtAsync<GetPlanVersionEndpoint>(new { planVersionId = result.Id }, result, cancellation: ct);
    }
}

/// <summary>
/// Replace a draft's entitlements. Every meter code is validated against the tenant's meter table,
/// and each allowance against that meter's kind.
/// </summary>
public class SetEntitlementsEndpoint : Endpoint<SetEntitlementsRequest, PlanVersionResponse>
{
    private readonly ICatalogueService _catalogue;

    public SetEntitlementsEndpoint(ICatalogueService catalogue) => _catalogue = catalogue;

    public override void Configure()
    {
        Put("/subscriptions/admin/plan-versions/{planVersionId:guid}/entitlements");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary = "Set a draft version's entitlements. Rejected once the version is published.");
    }

    public override async Task HandleAsync(SetEntitlementsRequest req, CancellationToken ct)
        => await Send.OkAsync(await _catalogue.SetEntitlementsAsync(Route<Guid>("planVersionId"), req, ct), ct);
}

/// <summary>
/// Publish a draft, superseding the plan's previous published version. Irreversible: after this the
/// price and entitlements are frozen, and a change means a new version.
/// </summary>
public class PublishVersionEndpoint : EndpointWithoutRequest<PlanVersionResponse>
{
    private readonly ICatalogueService _catalogue;

    public PublishVersionEndpoint(ICatalogueService catalogue) => _catalogue = catalogue;

    public override void Configure()
    {
        Post("/subscriptions/admin/plan-versions/{planVersionId:guid}/publish");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary = "Publish a draft version. Freezes its price and entitlements permanently.");
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await _catalogue.PublishVersionAsync(Route<Guid>("planVersionId"), ct), ct);
}

public class GetPlanVersionEndpoint : EndpointWithoutRequest<PlanVersionResponse>
{
    private readonly ICatalogueService _catalogue;

    public GetPlanVersionEndpoint(ICatalogueService catalogue) => _catalogue = catalogue;

    public override void Configure()
    {
        Get("/subscriptions/admin/plan-versions/{planVersionId:guid}");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary = "Read one plan version and its entitlements.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await _catalogue.GetVersionAsync(Route<Guid>("planVersionId"), ct);

        if (result is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result, ct);
    }
}

/// <summary>The version a new subscription would pin.</summary>
public class GetCurrentPlanVersionEndpoint : EndpointWithoutRequest<PlanVersionResponse>
{
    private readonly ICatalogueService _catalogue;

    public GetCurrentPlanVersionEndpoint(ICatalogueService catalogue) => _catalogue = catalogue;

    public override void Configure()
    {
        Get("/subscriptions/admin/plans/{planId:guid}/current-version");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary = "The published version a new subscription would pin.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await _catalogue.GetCurrentVersionAsync(Route<Guid>("planId"), ct);

        if (result is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result, ct);
    }
}
