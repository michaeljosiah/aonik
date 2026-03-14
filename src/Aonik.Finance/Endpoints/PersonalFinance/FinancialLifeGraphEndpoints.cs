using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.PersonalFinance;

internal sealed class GetFinancialLifeGraphEndpoint : EndpointWithoutRequest<FinancialLifeGraphResponse>
{
    private readonly IFinancialLifeGraphService _financialLifeGraphService;

    public GetFinancialLifeGraphEndpoint(IFinancialLifeGraphService financialLifeGraphService)
    {
        _financialLifeGraphService = financialLifeGraphService;
    }

    public override void Configure()
    {
        Get("/personal-finance/graph");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var response = await _financialLifeGraphService.GetGraphAsync(ct);
        await Send.OkAsync(response, ct);
    }
}

internal sealed class GetFinancialLifeGraphSummaryEndpoint : EndpointWithoutRequest<FinancialLifeGraphSummaryResponse>
{
    private readonly IFinancialLifeGraphService _financialLifeGraphService;

    public GetFinancialLifeGraphSummaryEndpoint(IFinancialLifeGraphService financialLifeGraphService)
    {
        _financialLifeGraphService = financialLifeGraphService;
    }

    public override void Configure()
    {
        Get("/personal-finance/graph/summary");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var response = await _financialLifeGraphService.GetGraphSummaryAsync(ct);
        await Send.OkAsync(response, ct);
    }
}

internal sealed class UpcomingObligationsRequest
{
    public int WithinDays { get; set; } = 30;
}

internal sealed class GetFinancialLifeUpcomingObligationsEndpoint : Endpoint<UpcomingObligationsRequest, IReadOnlyList<UpcomingObligationResponse>>
{
    private readonly IFinancialLifeGraphService _financialLifeGraphService;

    public GetFinancialLifeUpcomingObligationsEndpoint(IFinancialLifeGraphService financialLifeGraphService)
    {
        _financialLifeGraphService = financialLifeGraphService;
    }

    public override void Configure()
    {
        Get("/personal-finance/graph/upcoming-obligations");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(UpcomingObligationsRequest req, CancellationToken ct)
    {
        try
        {
            var response = await _financialLifeGraphService.GetUpcomingObligationsAsync(req.WithinDays, ct);
            await Send.OkAsync(response, ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message, 422);
        }
    }
}

internal sealed class CreateFinancialLifeGraphNodeEndpoint : Endpoint<CreateFinancialLifeGraphNodeRequest, FinancialLifeGraphNodeWriteResponse>
{
    private readonly Services.PersonalFinance.FinancialLifeGraphWriteService _writeService;

    public CreateFinancialLifeGraphNodeEndpoint(Services.PersonalFinance.FinancialLifeGraphWriteService writeService)
    {
        _writeService = writeService;
    }

    public override void Configure()
    {
        Post("/personal-finance/graph/nodes");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(CreateFinancialLifeGraphNodeRequest req, CancellationToken ct)
    {
        try
        {
            var response = await _writeService.CreateNodeAsync(req, ct);
            await Send.OkAsync(response, ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message, 422);
        }
        catch (InvalidOperationException ex)
        {
            ThrowError(ex.Message, 409);
        }
    }
}

internal sealed class CreateFinancialLifeGraphEdgeEndpoint : Endpoint<CreateFinancialLifeGraphEdgeRequest, FinancialLifeGraphEdgeWriteResponse>
{
    private readonly Services.PersonalFinance.FinancialLifeGraphWriteService _writeService;

    public CreateFinancialLifeGraphEdgeEndpoint(Services.PersonalFinance.FinancialLifeGraphWriteService writeService)
    {
        _writeService = writeService;
    }

    public override void Configure()
    {
        Post("/personal-finance/graph/edges");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(CreateFinancialLifeGraphEdgeRequest req, CancellationToken ct)
    {
        try
        {
            var response = await _writeService.CreateEdgeAsync(req, ct);
            await Send.OkAsync(response, ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message, 422);
        }
        catch (InvalidOperationException ex)
        {
            ThrowError(ex.Message, 409);
        }
    }
}

internal sealed class DeleteFinancialLifeGraphNodeEndpoint : EndpointWithoutRequest
{
    private readonly Services.PersonalFinance.FinancialLifeGraphWriteService _writeService;

    public DeleteFinancialLifeGraphNodeEndpoint(Services.PersonalFinance.FinancialLifeGraphWriteService writeService)
    {
        _writeService = writeService;
    }

    public override void Configure()
    {
        Delete("/personal-finance/graph/nodes/{id:guid}");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        try
        {
            await _writeService.DeleteNodeAsync(Route<Guid>("id"), ct);
            await Send.NoContentAsync(ct);
        }
        catch (InvalidOperationException ex)
        {
            ThrowError(ex.Message, 404);
        }
    }
}

internal sealed class DeleteFinancialLifeGraphEdgeEndpoint : EndpointWithoutRequest
{
    private readonly Services.PersonalFinance.FinancialLifeGraphWriteService _writeService;

    public DeleteFinancialLifeGraphEdgeEndpoint(Services.PersonalFinance.FinancialLifeGraphWriteService writeService)
    {
        _writeService = writeService;
    }

    public override void Configure()
    {
        Delete("/personal-finance/graph/edges/{id:guid}");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        try
        {
            await _writeService.DeleteEdgeAsync(Route<Guid>("id"), ct);
            await Send.NoContentAsync(ct);
        }
        catch (InvalidOperationException ex)
        {
            ThrowError(ex.Message, 404);
        }
    }
}
