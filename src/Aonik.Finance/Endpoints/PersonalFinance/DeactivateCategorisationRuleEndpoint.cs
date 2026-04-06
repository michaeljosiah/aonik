using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

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
        Summary(s =>
        {
            s.Summary = "Deactivate a categorisation rule";
            s.Description = "Deactivates an existing categorisation rule so it no longer applies to future transaction classifications.";
            s.Response(204, "Rule deactivated successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Rule not found");
        });
        Options(x => x.WithTags("Personal Finance"));
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
