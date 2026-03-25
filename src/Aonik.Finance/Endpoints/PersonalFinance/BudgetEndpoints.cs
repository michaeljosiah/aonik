using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.PersonalFinance;

// ── List Budgets ───────────────────────────────────────────────────────

internal sealed class ListBudgetsEndpoint
    : EndpointWithoutRequest<IReadOnlyList<BudgetCategoryResponse>>
{
    private readonly IBudgetService _service;

    public ListBudgetsEndpoint(IBudgetService service) => _service = service;

    public override void Configure()
    {
        Get("/personal-finance/budgets");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var response = await _service.ListBudgetsAsync(ct);
        await Send.OkAsync(response, ct);
    }
}

// ── Create Budget ──────────────────────────────────────────────────────

internal sealed class CreateBudgetEndpoint
    : Endpoint<CreateBudgetRequest, BudgetCategoryResponse>
{
    private readonly IBudgetService _service;

    public CreateBudgetEndpoint(IBudgetService service) => _service = service;

    public override void Configure()
    {
        Post("/personal-finance/budgets");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(CreateBudgetRequest req, CancellationToken ct)
    {
        try
        {
            var response = await _service.CreateBudgetAsync(req, ct);
            await Send.OkAsync(response, ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message, 422);
        }
    }
}

// ── Update Budget Amount ───────────────────────────────────────────────

internal sealed class UpdateBudgetAmountEndpoint
    : Endpoint<UpdateBudgetAmountRequest, IReadOnlyList<BudgetCategoryResponse>>
{
    private readonly IBudgetService _service;

    public UpdateBudgetAmountEndpoint(IBudgetService service) => _service = service;

    public override void Configure()
    {
        Put("/personal-finance/budgets/{budgetId}/amount");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(UpdateBudgetAmountRequest req, CancellationToken ct)
    {
        var budgetId = Route<Guid>("budgetId");

        try
        {
            var response = await _service.UpdateBudgetAmountAsync(budgetId, req, ct);
            await Send.OkAsync(response, ct);
        }
        catch (InvalidOperationException)
        {
            await Send.NotFoundAsync(ct);
        }
    }
}

// ── Delete Budget ──────────────────────────────────────────────────────

internal sealed class DeleteBudgetEndpoint
    : EndpointWithoutRequest<IReadOnlyList<BudgetCategoryResponse>>
{
    private readonly IBudgetService _service;

    public DeleteBudgetEndpoint(IBudgetService service) => _service = service;

    public override void Configure()
    {
        Delete("/personal-finance/budgets/{budgetId}");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var budgetId = Route<Guid>("budgetId");

        try
        {
            var response = await _service.DeleteBudgetAsync(budgetId, ct);
            await Send.OkAsync(response, ct);
        }
        catch (InvalidOperationException)
        {
            await Send.NotFoundAsync(ct);
        }
    }
}
