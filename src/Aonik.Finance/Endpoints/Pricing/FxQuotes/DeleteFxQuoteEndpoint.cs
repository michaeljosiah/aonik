using Aonik.Finance.Contracts.Services.Pricing;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Pricing.FxQuotes;

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
        Summary(s =>
        {
            s.Summary = "Delete an FX quote";
            s.Description = "Removes an FX quote from the system.";
            s.Response(204, "FX quote deleted successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "FX quote not found");
        });
        Options(x => x.WithTags("Pricing"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");

        await _fxQuoteService.DeleteAsync(id, ct);

        await Send.NoContentAsync(ct);
    }
}
