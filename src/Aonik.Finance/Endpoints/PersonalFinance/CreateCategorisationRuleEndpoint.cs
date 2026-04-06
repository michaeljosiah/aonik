using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

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
        Summary(s =>
        {
            s.Summary = "Create a categorisation rule";
            s.Description = "Creates a new rule for automatically categorising personal transactions based on merchant name, description, or other matching criteria.";
            s.Response(200, "Categorisation rule created successfully");
            s.Response(401, "Not authenticated");
            s.Response(409, "Conflicting rule already exists");
            s.Response(422, "Validation error");
        });
        Options(x => x.WithTags("Personal Finance"));
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
