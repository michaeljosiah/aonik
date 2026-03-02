using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.PersonalFinance;

internal sealed class DeactivateCategorisationRuleEndpoint : EndpointWithoutRequest
{
    private readonly ITransactionClassificationService _classificationService;

    public DeactivateCategorisationRuleEndpoint(ITransactionClassificationService classificationService)
    {
        _classificationService = classificationService;
    }

    public override void Configure()
    {
        Post("/personal-finance/classification/rules/{id}/deactivate");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");

        try
        {
            await _classificationService.DeactivateRuleAsync(id, ct);
            await Send.NoContentAsync(ct);
        }
        catch (InvalidOperationException)
        {
            await Send.NotFoundAsync(ct);
        }
    }
}
