using Aonik.Finance.Contracts.Models.Insights;
using Aonik.Finance.Contracts.Services.Insights;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.Insights;

public class GetMySpaceSummaryEndpoint : EndpointWithoutRequest<MySpaceSummaryResponse>
{
    private readonly IMySpaceSummaryService _service;

    public GetMySpaceSummaryEndpoint(IMySpaceSummaryService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("/insights/myspace-summary");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await _service.GetSummaryAsync(ct);
        await Send.OkAsync(result, ct);
    }
}
