using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Contracts.Services;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.PersonalFinance.Endpoints;

// ── List Bills ──────────────────────────────────────────────────

internal sealed class ListBillsRequest
{
    public string? Status { get; set; }
}

internal sealed class ListBillsEndpoint : Endpoint<ListBillsRequest, IReadOnlyList<BillResponse>>
{
    private readonly IBillService _billService;

    public ListBillsEndpoint(IBillService billService) => _billService = billService;

    public override void Configure()
    {
        Get("/personal-finance/bills");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "List bills";
            s.Description = "Returns all personal bills for the authenticated user, with an optional status filter (e.g. active, archived).";
            s.Response(200, "Bill list returned successfully");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(ListBillsRequest req, CancellationToken ct)
    {
        var response = await _billService.ListBillsAsync(req.Status, ct);
        await Send.OkAsync(response, ct);
    }
}

// ── Get Upcoming Bills ──────────────────────────────────────────

internal sealed class GetUpcomingBillsRequest
{
    public int Days { get; set; } = 7;
}

internal sealed class GetUpcomingBillsEndpoint : Endpoint<GetUpcomingBillsRequest, IReadOnlyList<BillResponse>>
{
    private readonly IBillService _billService;

    public GetUpcomingBillsEndpoint(IBillService billService) => _billService = billService;

    public override void Configure()
    {
        Get("/personal-finance/bills/upcoming");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Get upcoming bills";
            s.Description = "Returns bills due within the specified number of days, defaulting to the next 7 days.";
            s.Response(200, "Upcoming bills returned successfully");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(GetUpcomingBillsRequest req, CancellationToken ct)
    {
        var response = await _billService.GetUpcomingBillsAsync(req.Days, ct);
        await Send.OkAsync(response, ct);
    }
}

// ── Get Bill ────────────────────────────────────────────────────

internal sealed class GetBillRequest
{
    public Guid BillId { get; set; }
}

internal sealed class GetBillEndpoint : Endpoint<GetBillRequest, BillResponse>
{
    private readonly IBillService _billService;

    public GetBillEndpoint(IBillService billService) => _billService = billService;

    public override void Configure()
    {
        Get("/personal-finance/bills/{BillId}");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Get a bill by ID";
            s.Description = "Returns the full details of a single bill including payee, frequency, next due date, and autopay settings.";
            s.Response(200, "Bill returned successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Bill not found");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(GetBillRequest req, CancellationToken ct)
    {
        var response = await _billService.GetBillAsync(req.BillId, ct);

        if (response == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(response, ct);
    }
}

// ── Create Bill ─────────────────────────────────────────────────

internal sealed class CreateBillEndpoint : Endpoint<CreateBillRequest, BillResponse>
{
    private readonly IBillService _billService;

    public CreateBillEndpoint(IBillService billService) => _billService = billService;

    public override void Configure()
    {
        Post("/personal-finance/bills");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Create a bill";
            s.Description = "Creates a new recurring bill with payee, frequency, expected amount, and optional autopay configuration.";
            s.Response(201, "Bill created successfully");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CreateBillRequest req, CancellationToken ct)
    {
        var response = await _billService.CreateBillAsync(req, ct);
        await Send.CreatedAtAsync<GetBillEndpoint>(
            routeValues: new { BillId = response.BillId },
            responseBody: response,
            cancellation: ct);
    }
}

// ── Update Bill ─────────────────────────────────────────────────

internal sealed class UpdateBillRouteRequest
{
    public Guid BillId { get; set; }
    public string Payee { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public DateTime NextDueDate { get; set; }
    public decimal? ExpectedAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public bool Autopay { get; set; }
    public Guid? PaidFromAccountId { get; set; }
    public string Status { get; set; } = string.Empty;
}

internal sealed class UpdateBillEndpoint : Endpoint<UpdateBillRouteRequest, BillResponse>
{
    private readonly IBillService _billService;

    public UpdateBillEndpoint(IBillService billService) => _billService = billService;

    public override void Configure()
    {
        Put("/personal-finance/bills/{BillId}");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Update a bill";
            s.Description = "Updates all fields of an existing bill including payee, frequency, due date, amount, and autopay settings.";
            s.Response(200, "Bill updated successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Bill not found");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(UpdateBillRouteRequest req, CancellationToken ct)
    {
        var updateRequest = new UpdateBillRequest(
            req.Payee,
            req.Frequency,
            req.NextDueDate,
            req.ExpectedAmount,
            req.Currency,
            req.Autopay,
            req.PaidFromAccountId,
            req.Status);

        var response = await _billService.UpdateBillAsync(req.BillId, updateRequest, ct);
        await Send.OkAsync(response, ct);
    }
}

// ── Archive Bill ────────────────────────────────────────────────

internal sealed class ArchiveBillRequest
{
    public Guid BillId { get; set; }
}

internal sealed class ArchiveBillEndpoint : Endpoint<ArchiveBillRequest>
{
    private readonly IBillService _billService;

    public ArchiveBillEndpoint(IBillService billService) => _billService = billService;

    public override void Configure()
    {
        Post("/personal-finance/bills/{BillId}/archive");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Archive a bill";
            s.Description = "Archives a bill, removing it from active views while preserving its history for reporting purposes.";
            s.Response(204, "Bill archived successfully");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(ArchiveBillRequest req, CancellationToken ct)
    {
        await _billService.ArchiveBillAsync(req.BillId, ct);
        await Send.NoContentAsync(ct);
    }
}
