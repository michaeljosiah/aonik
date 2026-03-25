using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.PersonalFinance;

// ── Create Financial Context ────────────────────────────────────────────

internal sealed class CreateFinancialContextEndpoint
    : Endpoint<CreateFinancialContextRequest, FinancialContextResponse>
{
    private readonly IFinancialContextService _service;

    public CreateFinancialContextEndpoint(IFinancialContextService service) => _service = service;

    public override void Configure()
    {
        Post("/personal-finance/contexts");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(CreateFinancialContextRequest req, CancellationToken ct)
    {
        try
        {
            var response = await _service.CreateContextAsync(req, ct);
            await Send.OkAsync(response, ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message, 422);
        }
    }
}

// ── List Financial Contexts ─────────────────────────────────────────────

internal sealed class ListFinancialContextsRequest
{
    public bool IncludeArchived { get; set; }
}

internal sealed class ListFinancialContextsEndpoint
    : Endpoint<ListFinancialContextsRequest, IReadOnlyList<FinancialContextResponse>>
{
    private readonly IFinancialContextService _service;

    public ListFinancialContextsEndpoint(IFinancialContextService service) => _service = service;

    public override void Configure()
    {
        Get("/personal-finance/contexts");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(ListFinancialContextsRequest req, CancellationToken ct)
    {
        var response = await _service.ListContextsAsync(req.IncludeArchived, ct);
        await Send.OkAsync(response, ct);
    }
}

// ── Get Financial Context ───────────────────────────────────────────────

internal sealed class GetFinancialContextEndpoint
    : EndpointWithoutRequest<FinancialContextResponse>
{
    private readonly IFinancialContextService _service;

    public GetFinancialContextEndpoint(IFinancialContextService service) => _service = service;

    public override void Configure()
    {
        Get("/personal-finance/contexts/{id}");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var response = await _service.GetContextAsync(id, ct);

        if (response == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(response, ct);
    }
}

// ── Update Financial Context ────────────────────────────────────────────

internal sealed class UpdateFinancialContextEndpoint
    : Endpoint<UpdateFinancialContextRequest, FinancialContextResponse>
{
    private readonly IFinancialContextService _service;

    public UpdateFinancialContextEndpoint(IFinancialContextService service) => _service = service;

    public override void Configure()
    {
        Patch("/personal-finance/contexts/{id}");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(UpdateFinancialContextRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");

        try
        {
            var response = await _service.UpdateContextAsync(id, req, ct);
            await Send.OkAsync(response, ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message, 422);
        }
        catch (InvalidOperationException)
        {
            await Send.NotFoundAsync(ct);
        }
    }
}

// ── Archive Financial Context ───────────────────────────────────────────

internal sealed class ArchiveFinancialContextEndpoint : EndpointWithoutRequest
{
    private readonly IFinancialContextService _service;

    public ArchiveFinancialContextEndpoint(IFinancialContextService service) => _service = service;

    public override void Configure()
    {
        Post("/personal-finance/contexts/{id}/archive");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");

        try
        {
            await _service.ArchiveContextAsync(id, ct);
            await Send.NoContentAsync(ct);
        }
        catch (InvalidOperationException)
        {
            await Send.NotFoundAsync(ct);
        }
    }
}

// ── Add Funding Source ──────────────────────────────────────────────────

internal sealed class AddFundingSourceEndpoint
    : Endpoint<AddFundingSourceRequest, FinancialContextFundingSourceResponse>
{
    private readonly IFinancialContextService _service;

    public AddFundingSourceEndpoint(IFinancialContextService service) => _service = service;

    public override void Configure()
    {
        Post("/personal-finance/contexts/{id}/funding-sources");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(AddFundingSourceRequest req, CancellationToken ct)
    {
        var contextId = Route<Guid>("id");

        try
        {
            var response = await _service.AddFundingSourceAsync(contextId, req, ct);
            await Send.OkAsync(response, ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message, 422);
        }
        catch (InvalidOperationException)
        {
            await Send.NotFoundAsync(ct);
        }
    }
}

// ── Remove Funding Source ───────────────────────────────────────────────

internal sealed class RemoveFundingSourceEndpoint : EndpointWithoutRequest
{
    private readonly IFinancialContextService _service;

    public RemoveFundingSourceEndpoint(IFinancialContextService service) => _service = service;

    public override void Configure()
    {
        Delete("/personal-finance/contexts/{id}/funding-sources/{fundingSourceId}");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var contextId = Route<Guid>("id");
        var fundingSourceId = Route<Guid>("fundingSourceId");

        try
        {
            await _service.RemoveFundingSourceAsync(contextId, fundingSourceId, ct);
            await Send.NoContentAsync(ct);
        }
        catch (InvalidOperationException)
        {
            await Send.NotFoundAsync(ct);
        }
    }
}

// ── Assign Transaction Context ──────────────────────────────────────────

internal sealed class AssignTransactionContextEndpoint
    : Endpoint<AssignTransactionContextRequest>
{
    private readonly IFinancialContextService _service;

    public AssignTransactionContextEndpoint(IFinancialContextService service) => _service = service;

    public override void Configure()
    {
        Put("/personal-finance/transactions/{id}/context");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(AssignTransactionContextRequest req, CancellationToken ct)
    {
        var transactionId = Route<Guid>("id");

        try
        {
            await _service.AssignTransactionContextAsync(transactionId, req, ct);
            await Send.NoContentAsync(ct);
        }
        catch (InvalidOperationException)
        {
            await Send.NotFoundAsync(ct);
        }
    }
}

// ── Context Summary ─────────────────────────────────────────────────────

internal sealed class GetFinancialContextSummaryRequest
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}

internal sealed class GetFinancialContextSummaryEndpoint
    : Endpoint<GetFinancialContextSummaryRequest, FinancialContextSummaryResponse>
{
    private readonly IFinancialContextService _service;

    public GetFinancialContextSummaryEndpoint(IFinancialContextService service) => _service = service;

    public override void Configure()
    {
        Get("/personal-finance/contexts/{id}/summary");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(GetFinancialContextSummaryRequest req, CancellationToken ct)
    {
        var contextId = Route<Guid>("id");

        try
        {
            var response = await _service.GetContextSummaryAsync(contextId, req.From, req.To, ct);
            await Send.OkAsync(response, ct);
        }
        catch (InvalidOperationException)
        {
            await Send.NotFoundAsync(ct);
        }
    }
}
