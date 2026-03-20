using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Services.PersonalFinance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.PersonalFinance;

/// <summary>
/// Returns the canonical transaction category taxonomy. This is the single source of truth
/// for category codes, display names, icons, and groupings. Clients should fetch this on
/// startup rather than hardcoding category lists.
/// </summary>
internal sealed class ListTransactionCategoriesEndpoint : EndpointWithoutRequest<TransactionCategoryListResponse>
{
    public override void Configure()
    {
        Get("/personal-finance/categories");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var allCategories = TransactionCategoryReference.GetAllCategories();

        var categoryResponses = allCategories
            .Select(cat =>
            {
                var subCategories = TransactionCategoryReference.GetSubCategories(cat.Code);
                var subCategoryResponses = subCategories.Count > 0
                    ? subCategories.Select(sc => new TransactionSubCategoryResponse(
                        sc.Code, sc.DisplayName, sc.IconName, sc.SortOrder)).ToList()
                    : null;

                return new TransactionCategoryResponse(
                    cat.Code,
                    cat.DisplayName,
                    cat.GroupName,
                    cat.IconName,
                    cat.SortOrder,
                    subCategoryResponses);
            })
            .ToList();

        var groups = categoryResponses
            .GroupBy(c => c.GroupName)
            .OrderBy(g => g.Min(c => c.SortOrder))
            .Select(g => new TransactionCategoryGroupResponse(g.Key, g.OrderBy(c => c.SortOrder).ToList()))
            .ToList();

        var response = new TransactionCategoryListResponse(groups, categoryResponses);

        await Send.OkAsync(response, ct);
    }
}
