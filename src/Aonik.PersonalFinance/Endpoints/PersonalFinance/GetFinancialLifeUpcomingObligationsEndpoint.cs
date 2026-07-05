using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Contracts.Services;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.PersonalFinance.Endpoints;

internal sealed class UpcomingObligationsRequest
{
    public int WithinDays { get; set; } = 30;
}

internal sealed class GetFinancialLifeUpcomingObligationsEndpoint : Endpoint<UpcomingObligationsRequest, IReadOnlyList<UpcomingObligationResponse>>
{
    private readonly IFinancialLifeGraphService _financialLifeGraphService;

    public GetFinancialLifeUpcomingObligationsEndpoint(IFinancialLifeGraphService financialLifeGraphService)
    {
        _financialLifeGraphService = financialLifeGraphService;
    }

    public override void Configure()
    {
        Get("/personal-finance/graph/upcoming-obligations");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Get upcoming financial obligations";
            s.Description = "Returns upcoming bills, subscriptions, and other recurring financial obligations within the specified number of days.";
            s.Response(200, "Upcoming obligations returned successfully");
            s.Response(401, "Not authenticated");
            s.Response(422, "Validation error");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(UpcomingObligationsRequest req, CancellationToken ct)
    {
        try
        {
            var response = await _financialLifeGraphService.GetUpcomingObligationsAsync(req.WithinDays, ct);
            await Send.OkAsync(response, ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message, 422);
        }
    }
}
