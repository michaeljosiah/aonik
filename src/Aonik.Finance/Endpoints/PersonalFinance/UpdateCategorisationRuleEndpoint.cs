using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.PersonalFinance;

internal sealed class UpdateCategorisationRuleEndpoint : Endpoint<UpdateCategorisationRuleRequest, CategorisationRuleResponse>
{
    private readonly ITransactionClassificationService _classificationService;

    public UpdateCategorisationRuleEndpoint(ITransactionClassificationService classificationService)
    {
        _classificationService = classificationService;
    }

    public override void Configure()
    {
        Patch("/personal-finance/classification/rules/{id}");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(UpdateCategorisationRuleRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");

        try
        {
            var response = await _classificationService.UpdateRuleAsync(id, req, ct);
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
