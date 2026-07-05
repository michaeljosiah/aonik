using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Services;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.PersonalFinance.Endpoints.Admin;

internal sealed class AdminGetFinancialLifeGraphRequest
{
    public Guid UserId { get; set; }
}

/// <summary>
/// Admin endpoint to retrieve the full financial life graph for a specific user.
/// Uses the same hydration/projection pipeline as the user-facing endpoint but
/// accepts an explicit userId instead of relying on the current user context.
/// </summary>
internal sealed class AdminGetFinancialLifeGraphEndpoint
    : Endpoint<AdminGetFinancialLifeGraphRequest, FinancialLifeGraphResponse>
{
    private readonly FinancialLifeGraphLoader _loader;
    private readonly ITenantProvider _tenantProvider;

    public AdminGetFinancialLifeGraphEndpoint(
        FinancialLifeGraphLoader loader,
        ITenantProvider tenantProvider)
    {
        _loader = loader;
        _tenantProvider = tenantProvider;
    }

    public override void Configure()
    {
        Get("/admin/personal-finance/users/{UserId:guid}/graph");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Get financial life graph for a user (admin)";
            s.Description = "Returns the full financial life graph for the specified user, including projected nodes from accounts, transactions, bills, goals, and native graph annotations.";
            s.Response(200, "Graph returned");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(AdminGetFinancialLifeGraphRequest req, CancellationToken ct)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var coreSnapshot = await _loader.LoadCoreSnapshotAsync(
            tenantId, req.UserId, FinancialLifeGraphHydrationService.TransactionWindowDays, ct);

        var relevantCurrencies = FinancialLifeGraphLoader.GetRelevantAccountCurrencies(
            coreSnapshot.Accounts, coreSnapshot.LinkedAccounts);
        var fxQuotes = await _loader.LoadFxQuotesAsync(tenantId, relevantCurrencies, ct);

        var fullSnapshot = coreSnapshot with { FxQuotes = fxQuotes };

        var response = FinancialLifeGraphService.BuildGraphFromSnapshot(fullSnapshot);
        await Send.OkAsync(response, ct);
    }
}
