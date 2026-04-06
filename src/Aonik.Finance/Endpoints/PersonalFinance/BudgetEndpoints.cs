using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

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
        Summary(s =>
        {
            s.Summary = "List all budgets";
            s.Description = "Returns all budget categories with their spending limits and current period usage for the authenticated user.";
            s.Response(200, "Budget list returned successfully");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Personal Finance"));
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
        Summary(s =>
        {
            s.Summary = "Create a budget";
            s.Description = "Creates a new budget with a spending limit for a specific transaction category.";
            s.Response(200, "Budget created successfully");
            s.Response(401, "Not authenticated");
            s.Response(422, "Validation error");
        });
        Options(x => x.WithTags("Personal Finance"));
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
        Summary(s =>
        {
            s.Summary = "Update a budget amount";
            s.Description = "Updates the spending limit amount for an existing budget category.";
            s.Response(200, "Budget amount updated successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Budget not found");
        });
        Options(x => x.WithTags("Personal Finance"));
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
        Summary(s =>
        {
            s.Summary = "Delete a budget";
            s.Description = "Removes a budget category and returns the remaining budgets for the authenticated user.";
            s.Response(200, "Budget deleted successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Budget not found");
        });
        Options(x => x.WithTags("Personal Finance"));
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
