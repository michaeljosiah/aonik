using Aonik.Commerce.Contracts.Models.Fulfilment;
using Aonik.Commerce.Services.Fulfilment;

using FastEndpoints;
using Microsoft.AspNetCore.Builder;

namespace Aonik.Commerce.Endpoints.Public.Fulfilment;

public sealed class GetDeliveryCoverageEndpoint(IDeliveryCoverageService coverage) : EndpointWithoutRequest<DeliveryCoverageDto>
{
    public const string RateLimitPolicyName = "commerce-delivery-lookup";

    public override void Configure()
    {
        Get("/commerce/delivery/coverage");
        AllowAnonymous();
        Options(builder => builder.RequireRateLimiting(RateLimitPolicyName));
        Summary(summary => summary.Summary = "Check a current postcode against the tenant's configured delivery coverage.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var postcodes = HttpContext.Request.Query["postcode"];
        var result = await coverage.CheckAsync(postcodes.Count == 1 ? postcodes[0] : null, ct);
        await Send.OkAsync(result, ct);
    }
}
