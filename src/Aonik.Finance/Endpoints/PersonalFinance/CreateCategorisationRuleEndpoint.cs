using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.PersonalFinance;

internal sealed class CreateCategorisationRuleEndpoint : Endpoint<CreateCategorisationRuleRequest, CategorisationRuleResponse>
{
    private readonly ITransactionClassificationService _classificationService;

    public CreateCategorisationRuleEndpoint(ITransactionClassificationService classificationService)
    {
        _classificationService = classificationService;
    }

    public override void Configure()
    {
        Post("/personal-finance/classification/rules");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(CreateCategorisationRuleRequest req, CancellationToken ct)
    {
        try
        {
            var response = await _classificationService.CreateRuleAsync(req, ct);
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
