using Aonik.Application.Services.Pricing;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Pricing.FxQuotes;

public class DeleteFxQuoteEndpoint : EndpointWithoutRequest
{
    private readonly IFxQuoteService _fxQuoteService;

    public DeleteFxQuoteEndpoint(IFxQuoteService fxQuoteService)
    {
        _fxQuoteService = fxQuoteService;
    }

    public override void Configure()
    {
        Delete("/fx-quotes/{id}");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");

        await _fxQuoteService.DeleteAsync(id, ct);

        await Send.NoContentAsync(ct);
    }
}
