using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;

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
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var response = await _classificationService.ListRulesAsync(ct);
        await Send.OkAsync(response, ct);
    }
}
