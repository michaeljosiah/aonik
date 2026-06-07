using Aonik.Platform.Contracts.Api.Settings;
using Aonik.Platform.Contracts.Models.Settings;
using Aonik.Platform.Contracts.Services.Settings;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Settings;

internal class UpdatePaymentGatewaySettingsEndpoint
    : Endpoint<PaymentGatewaySettingsUpdateRequest, PaymentGatewaySettingsResponse>
{
    private readonly IPaymentGatewaySettingsService _service;

    public UpdatePaymentGatewaySettingsEndpoint(IPaymentGatewaySettingsService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Put("/admin/settings/payment-gateways");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Update payment gateway settings";
            s.Description = "Updates gateway settings. Secret fields are write-only; null/blank keeps existing secrets.";
            s.Response(200, "Settings updated");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Settings"));
    }

    public override async Task HandleAsync(PaymentGatewaySettingsUpdateRequest req, CancellationToken ct)
    {
        try
        {
            var update = new PaymentGatewaySettingsUpdate(req.Providers.Select(provider =>
                new PaymentGatewayProviderUpdate(
                    provider.ProviderCode,
                    provider.Enabled,
                    provider.BaseUrl,
                    provider.IdpTokenUrl,
                    provider.ClientId,
                    provider.DefaultTransferPurpose,
                    provider.ClientSecret,
                    provider.EncryptionKey,
                    provider.SigningSecret)).ToList());

            var snapshot = await _service.UpdateAsync(update, ct);
            await Send.OkAsync(GetPaymentGatewaySettingsEndpoint.MapResponse(snapshot), ct);
        }
        catch (InvalidOperationException ex)
        {
            AddError(ex.Message);
            await Send.ErrorsAsync(400, ct);
        }
    }
}
