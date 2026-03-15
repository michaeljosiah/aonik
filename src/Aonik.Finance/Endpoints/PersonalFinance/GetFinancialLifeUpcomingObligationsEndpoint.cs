using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.PersonalFinance;

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
