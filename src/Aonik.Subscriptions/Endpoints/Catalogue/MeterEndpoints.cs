using Aonik.SharedKernel.Abstractions;
using Aonik.Subscriptions.Contracts.Models;
using Aonik.Subscriptions.Contracts.Services;

using FastEndpoints;

namespace Aonik.Subscriptions.Endpoints.Catalogue;

/// <summary>Register a meter — the unit a plan confers and usage draws down (Spec 087 §6).</summary>
public class CreateMeterEndpoint : Endpoint<CreateMeterRequest, MeterResponse>
{
    private readonly ICatalogueService _catalogue;

    public CreateMeterEndpoint(ICatalogueService catalogue) => _catalogue = catalogue;

    public override void Configure()
    {
        Post("/subscriptions/admin/meters");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary = "Register a meter. Its kind is fixed at creation.");
    }

    public override async Task HandleAsync(CreateMeterRequest req, CancellationToken ct)
    {
        var result = await _catalogue.CreateMeterAsync(req, ct);
        await Send.CreatedAtAsync<GetMeterEndpoint>(new { meterId = result.Id }, result, cancellation: ct);
    }
}

/// <summary>Amend a meter's presentation. Kind is immutable — see <see cref="ICatalogueService"/>.</summary>
public class UpdateMeterEndpoint : Endpoint<UpdateMeterRequest, MeterResponse>
{
    private readonly ICatalogueService _catalogue;

    public UpdateMeterEndpoint(ICatalogueService catalogue) => _catalogue = catalogue;

    public override void Configure()
    {
        Put("/subscriptions/admin/meters/{meterId:guid}");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary = "Update a meter's display name and unit.");
    }

    public override async Task HandleAsync(UpdateMeterRequest req, CancellationToken ct)
    {
        var result = await _catalogue.UpdateMeterAsync(Route<Guid>("meterId"), req, ct);
        await Send.OkAsync(result, ct);
    }
}

public class GetMeterEndpoint : EndpointWithoutRequest<MeterResponse>
{
    private readonly ICatalogueService _catalogue;

    public GetMeterEndpoint(ICatalogueService catalogue) => _catalogue = catalogue;

    public override void Configure()
    {
        Get("/subscriptions/admin/meters/{meterId:guid}");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary = "Read one meter.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await _catalogue.GetMeterAsync(Route<Guid>("meterId"), ct);

        if (result is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result, ct);
    }
}

public class ListMetersEndpoint : EndpointWithoutRequest<IReadOnlyList<MeterResponse>>
{
    private readonly ICatalogueService _catalogue;

    public ListMetersEndpoint(ICatalogueService catalogue) => _catalogue = catalogue;

    public override void Configure()
    {
        Get("/subscriptions/admin/meters");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary = "List the tenant's meters — the authority for valid meter codes.");
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await _catalogue.ListMetersAsync(ct), ct);
}
