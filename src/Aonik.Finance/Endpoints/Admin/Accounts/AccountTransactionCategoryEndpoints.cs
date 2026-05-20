using Aonik.Finance.Contracts.Models.Accounts;
using Aonik.Finance.Contracts.Services.Accounts;
using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Admin.Accounts;

/// <summary>
/// Spec 028 — manual category override, lock release, merchant-rule CRUD,
/// and bulk re-categorization for linked-account transactions.
/// </summary>
internal sealed class SetAccountTransactionCategoryEndpoint
    : Endpoint<SetAccountTransactionCategoryEndpoint.RouteRequest, AccountTransactionCategoryResult>
{
    private readonly IAccountLinkService _service;

    public SetAccountTransactionCategoryEndpoint(IAccountLinkService service)
    {
        _service = service;
    }

    public sealed class RouteRequest
    {
        public Guid Id { get; set; }
        public string Category { get; set; } = string.Empty;
        public string? SubCategory { get; set; }
        public bool RememberForMerchant { get; set; }
    }

    public override void Configure()
    {
        Patch("/admin/accounts/transactions/{id}/category");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Manually set a transaction category";
            s.Description = "Overrides the category on a single account transaction and locks "
                + "it against future sync overwrites. Optionally upserts a tenant merchant "
                + "rule so future transactions from the same merchant categorize the same way.";
            s.Response(200, "Category updated");
            s.Response(400, "Invalid category or sub-category code");
            s.Response(401, "Not authenticated");
            s.Response(404, "Transaction not found");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(RouteRequest req, CancellationToken ct)
    {
        try
        {
            var payload = new SetAccountTransactionCategoryRequest(
                req.Category,
                req.SubCategory,
                req.RememberForMerchant);

            var result = await _service.SetTransactionCategoryAsync(req.Id, payload, ct);
            if (result is null)
            {
                await Send.NotFoundAsync(ct);
                return;
            }

            await Send.OkAsync(result, ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message, 400);
        }
    }
}

internal sealed class UnlockAccountTransactionCategoryEndpoint : EndpointWithoutRequest
{
    private readonly IAccountLinkService _service;

    public UnlockAccountTransactionCategoryEndpoint(IAccountLinkService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Delete("/admin/accounts/transactions/{id}/category/lock");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Release the manual category lock";
            s.Description = "Clears CategoryLockedAt so future syncs may re-categorize the "
                + "transaction automatically. Does not clear the current category value.";
            s.Response(204, "Lock released");
            s.Response(401, "Not authenticated");
            s.Response(404, "Transaction not found");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var found = await _service.UnlockTransactionCategoryAsync(id, ct);
        if (!found)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}

internal sealed class ListMerchantCategoryRulesEndpoint
    : EndpointWithoutRequest<IReadOnlyList<MerchantCategoryResult>>
{
    private readonly IAccountLinkService _service;

    public ListMerchantCategoryRulesEndpoint(IAccountLinkService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("/admin/accounts/merchant-categories");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "List merchant category rules";
            s.Description = "Returns all per-tenant merchant-to-category rules used by the "
                + "sync categorizer.";
            s.Response(200, "Rules retrieved successfully");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var rules = await _service.ListMerchantCategoriesAsync(ct);
        await Send.OkAsync(rules, ct);
    }
}

internal sealed class DeleteMerchantCategoryRuleEndpoint : EndpointWithoutRequest
{
    private readonly IAccountLinkService _service;

    public DeleteMerchantCategoryRuleEndpoint(IAccountLinkService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Delete("/admin/accounts/merchant-categories/{id}");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Delete a merchant category rule";
            s.Description = "Removes a single per-tenant merchant-to-category rule.";
            s.Response(204, "Rule deleted");
            s.Response(401, "Not authenticated");
            s.Response(404, "Rule not found");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var deleted = await _service.DeleteMerchantCategoryAsync(id, ct);
        if (!deleted)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}

internal sealed class RecategorizeAccountTransactionsEndpoint
    : Endpoint<RecategorizeAccountTransactionsEndpoint.RouteRequest, RecategorizeAccountTransactionsResult>
{
    private readonly IAccountLinkService _service;

    public RecategorizeAccountTransactionsEndpoint(IAccountLinkService service)
    {
        _service = service;
    }

    public sealed class RouteRequest
    {
        public Guid ConnectionId { get; set; }
        public bool IncludeLocked { get; set; }
        public bool UnresolvedOnly { get; set; } = true;
    }

    public override void Configure()
    {
        Post("/admin/accounts/connections/{connectionId}/transactions/recategorize");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Bulk re-categorize transactions on a connection";
            s.Description = "Replays the classification pipeline against transactions belonging "
                + "to a linked-account connection. UnresolvedOnly (default true) skips rows that "
                + "already carry a confident provider-mapped or merchant-rule category. "
                + "IncludeLocked (default false) requires explicit opt-in and clears the manual "
                + "lock as a side effect. Intended to run once per connection at deployment.";
            s.Response(200, "Re-categorization complete");
            s.Response(401, "Not authenticated");
            s.Response(404, "Connection not found");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(RouteRequest req, CancellationToken ct)
    {
        var payload = new RecategorizeAccountTransactionsRequest(req.IncludeLocked, req.UnresolvedOnly);
        var result = await _service.RecategorizeTransactionsAsync(req.ConnectionId, payload, ct);
        if (result is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result, ct);
    }
}
