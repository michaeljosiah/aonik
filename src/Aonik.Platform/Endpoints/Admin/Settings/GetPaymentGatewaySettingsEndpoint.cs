using Aonik.Platform.Contracts.Api.Settings;
using Aonik.Platform.Contracts.Models.Settings;
using Aonik.Platform.Contracts.Services.Settings;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Settings;

internal class GetPaymentGatewaySettingsEndpoint : EndpointWithoutRequest<PaymentGatewaySettingsResponse>
{
    private readonly IPaymentGatewaySettingsService _service;

    public GetPaymentGatewaySettingsEndpoint(IPaymentGatewaySettingsService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("/admin/settings/payment-gateways");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Get payment gateway settings";
            s.Description = "Returns gateway settings without exposing secret values.";
            s.Response(200, "Payment gateway settings");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Settings"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var snapshot = await _service.GetAsync(ct);
        await Send.OkAsync(MapResponse(snapshot), ct);
    }

    internal static PaymentGatewaySettingsResponse MapResponse(PaymentGatewaySettingsSnapshot snapshot)
        => new(snapshot.Providers.Select(provider => new PaymentGatewayProviderResponse(
            provider.ProviderCode,
            provider.Enabled,
            provider.BaseUrl,
            provider.IdpTokenUrl,
            provider.ClientId,
            provider.DefaultTransferPurpose,
            provider.HasClientSecret,
            provider.HasEncryptionKey,
            provider.HasSigningSecret,
            provider.SecretSource)).ToList());
}
