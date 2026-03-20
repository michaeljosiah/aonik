using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.PersonalFinance;

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
    }

    public override async Task HandleAsync(ArchiveBillRequest req, CancellationToken ct)
    {
        await _billService.ArchiveBillAsync(req.BillId, ct);
        await Send.NoContentAsync(ct);
    }
}
