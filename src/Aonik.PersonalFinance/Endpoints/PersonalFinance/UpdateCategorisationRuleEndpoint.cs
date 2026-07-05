using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Contracts.Services;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.PersonalFinance.Endpoints;

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
        Summary(s =>
        {
            s.Summary = "Update a categorisation rule";
            s.Description = "Updates the matching criteria or target category of an existing categorisation rule.";
            s.Response(200, "Rule updated successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Rule not found");
            s.Response(422, "Validation error");
        });
        Options(x => x.WithTags("Personal Finance"));
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
