using Aonik.Subscriptions.Contracts.Models;
using Aonik.Subscriptions.Contracts.Services;

using FastEndpoints;

namespace Aonik.Subscriptions.Endpoints.Catalogue;

/// <summary>Create a plan. It becomes offerable when its first version is published (Spec 087 §6).</summary>
public class CreatePlanEndpoint : Endpoint<CreatePlanRequest, PlanResponse>
{
    private readonly ICatalogueService _catalogue;

    public CreatePlanEndpoint(ICatalogueService catalogue) => _catalogue = catalogue;

    public override void Configure()
    {
        Post("/subscriptions/admin/plans");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary = "Create a plan. Code and billing interval are fixed once created.");
    }

    public override async Task HandleAsync(CreatePlanRequest req, CancellationToken ct)
    {
        var result = await _catalogue.CreatePlanAsync(req, ct);
        await Send.CreatedAtAsync<GetPlanEndpoint>(new { planId = result.Id }, result, cancellation: ct);
    }
}

public class UpdatePlanEndpoint : Endpoint<UpdatePlanRequest, PlanResponse>
{
    private readonly ICatalogueService _catalogue;

    public UpdatePlanEndpoint(ICatalogueService catalogue) => _catalogue = catalogue;

    public override void Configure()
    {
        Put("/subscriptions/admin/plans/{planId:guid}");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary = "Update a plan's presentation. Price and entitlements live on a version.");
    }

    public override async Task HandleAsync(UpdatePlanRequest req, CancellationToken ct)
        => await Send.OkAsync(await _catalogue.UpdatePlanAsync(Route<Guid>("planId"), req, ct), ct);
}

public class GetPlanEndpoint : EndpointWithoutRequest<PlanResponse>
{
    private readonly ICatalogueService _catalogue;

    public GetPlanEndpoint(ICatalogueService catalogue) => _catalogue = catalogue;

    public override void Configure()
    {
        Get("/subscriptions/admin/plans/{planId:guid}");
        Policies("AdminReadPolicy");
        Summary(s => s.Summary = "Read a plan with all its versions and their entitlements.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await _catalogue.GetPlanAsync(Route<Guid>("planId"), ct);

        if (result is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result, ct);
    }
}

public class ListPlansEndpoint : EndpointWithoutRequest<IReadOnlyList<PlanResponse>>
{
    private readonly ICatalogueService _catalogue;

    public ListPlansEndpoint(ICatalogueService catalogue) => _catalogue = catalogue;

    public override void Configure()
    {
        Get("/subscriptions/admin/plans");
        Policies("AdminReadPolicy");
        Summary(s => s.Summary = "List plans. Retired plans are excluded unless includeRetired=true.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var includeRetired = Query<bool>("includeRetired", isRequired: false);
        await Send.OkAsync(await _catalogue.ListPlansAsync(includeRetired, ct), ct);
    }
}

/// <summary>Withdraw a plan from sale. Existing subscribers keep it and keep renewing.</summary>
public class RetirePlanEndpoint : EndpointWithoutRequest<PlanResponse>
{
    private readonly ICatalogueService _catalogue;

    public RetirePlanEndpoint(ICatalogueService catalogue) => _catalogue = catalogue;

    public override void Configure()
    {
        Post("/subscriptions/admin/plans/{planId:guid}/retire");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary = "Withdraw a plan from sale without affecting existing subscribers.");
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await _catalogue.RetirePlanAsync(Route<Guid>("planId"), ct), ct);
}
