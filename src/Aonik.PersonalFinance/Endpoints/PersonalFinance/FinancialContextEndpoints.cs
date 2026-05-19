using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

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
        Summary(s =>
        {
            s.Summary = "Create a financial context";
            s.Description = "Creates a new financial context (e.g. a project, trip, or event) for grouping and tracking related transactions.";
            s.Response(200, "Financial context created successfully");
            s.Response(401, "Not authenticated");
            s.Response(422, "Validation error");
        });
        Options(x => x.WithTags("Personal Finance"));
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
        Summary(s =>
        {
            s.Summary = "List financial contexts";
            s.Description = "Returns all financial contexts for the authenticated user, with an option to include archived contexts.";
            s.Response(200, "Financial contexts returned successfully");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Personal Finance"));
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
        Summary(s =>
        {
            s.Summary = "Get a financial context by ID";
            s.Description = "Returns the details of a single financial context including its name, funding sources, and associated transactions.";
            s.Response(200, "Financial context returned successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Financial context not found");
        });
        Options(x => x.WithTags("Personal Finance"));
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
        Summary(s =>
        {
            s.Summary = "Update a financial context";
            s.Description = "Partially updates a financial context's name, description, or other mutable properties.";
            s.Response(200, "Financial context updated successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Financial context not found");
            s.Response(422, "Validation error");
        });
        Options(x => x.WithTags("Personal Finance"));
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
        Summary(s =>
        {
            s.Summary = "Archive a financial context";
            s.Description = "Archives a financial context, hiding it from active views while preserving its data for historical reference.";
            s.Response(204, "Financial context archived successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Financial context not found");
        });
        Options(x => x.WithTags("Personal Finance"));
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
        Summary(s =>
        {
            s.Summary = "Add a funding source to a context";
            s.Description = "Links a personal account as a funding source for a financial context, enabling budget tracking against that account.";
            s.Response(200, "Funding source added successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Financial context not found");
            s.Response(422, "Validation error");
        });
        Options(x => x.WithTags("Personal Finance"));
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
        Summary(s =>
        {
            s.Summary = "Remove a funding source from a context";
            s.Description = "Unlinks a personal account as a funding source from a financial context.";
            s.Response(204, "Funding source removed successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Financial context or funding source not found");
        });
        Options(x => x.WithTags("Personal Finance"));
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
        Summary(s =>
        {
            s.Summary = "Assign a transaction to a context";
            s.Description = "Associates a personal transaction with a financial context for grouped tracking and reporting.";
            s.Response(204, "Transaction context assigned successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Transaction or context not found");
        });
        Options(x => x.WithTags("Personal Finance"));
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
        Summary(s =>
        {
            s.Summary = "Get financial context summary";
            s.Description = "Returns an aggregated spending summary for a financial context over an optional date range, including totals by category.";
            s.Response(200, "Context summary returned successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Financial context not found");
        });
        Options(x => x.WithTags("Personal Finance"));
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
