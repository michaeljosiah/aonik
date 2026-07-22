using Aonik.Commerce.Contracts.Api.Fulfilment;
using Aonik.Commerce.Contracts.Models.Fulfilment;
using Aonik.Commerce.Services.Fulfilment;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Fulfilment;

/// <summary>Spec 069 §6 — the current calendar, including inactive.</summary>
public class GetFulfilmentCalendarEndpoint : EndpointWithoutRequest<FulfilmentCalendarDto>
{
    private readonly IFulfilmentPromiseService _promises;

    public GetFulfilmentCalendarEndpoint(IFulfilmentPromiseService promises) => _promises = promises;

    public override void Configure()
    {
        Get("/commerce/admin/fulfilment-calendar");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary = "The tenant's fulfilment calendar (including inactive).");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var calendar = await _promises.GetCalendarAsync(ct);
        if (calendar is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        await Send.OkAsync(calendar, ct);
    }
}

/// <summary>Spec 069 §6 — upsert; the response echoes the computed current promise so the
/// operator sees the effect immediately (A5).</summary>
public class UpsertFulfilmentCalendarEndpoint : Endpoint<UpsertFulfilmentCalendarRequest, FulfilmentCalendarDto>
{
    private readonly IFulfilmentPromiseService _promises;

    public UpsertFulfilmentCalendarEndpoint(IFulfilmentPromiseService promises) => _promises = promises;

    public override void Configure()
    {
        Put("/commerce/admin/fulfilment-calendar");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary = "Upsert the tenant's fulfilment calendar.");
    }

    public override async Task HandleAsync(UpsertFulfilmentCalendarRequest req, CancellationToken ct)
    {
        var result = await _promises.UpsertCalendarAsync(new UpsertFulfilmentCalendarCommand(
            req.Timezone,
            req.DeliveryDays ?? [],
            req.CutoffLocalTime,
            req.CutoffDayOfWeek,
            req.LeadDays,
            req.BlackoutDates ?? [],
            req.IsActive), ct);
        await Send.OkAsync(result, ct);
    }
}
