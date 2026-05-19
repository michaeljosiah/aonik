using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.PersonalFinance;

internal sealed class ListCategorisationRulesEndpoint : EndpointWithoutRequest<IReadOnlyList<CategorisationRuleResponse>>
{
    private readonly ITransactionClassificationService _classificationService;

    public ListCategorisationRulesEndpoint(ITransactionClassificationService classificationService)
    {
        _classificationService = classificationService;
    }

    public override void Configure()
    {
        Get("/personal-finance/classification/rules");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "List all categorisation rules";
            s.Description = "Returns all active and inactive categorisation rules configured by the user for automatic transaction classification.";
            s.Response(200, "Categorisation rules returned successfully");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var response = await _classificationService.ListRulesAsync(ct);
        await Send.OkAsync(response, ct);
    }
}
