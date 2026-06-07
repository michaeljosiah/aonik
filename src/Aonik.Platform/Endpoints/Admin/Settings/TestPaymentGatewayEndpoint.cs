using Aonik.Platform.Contracts.Api.Settings;
using Aonik.Platform.Contracts.Services.Settings;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Settings;

internal class TestPaymentGatewayEndpoint : Endpoint<TestPaymentGatewayRequest, TestPaymentGatewayResponse>
{
    private readonly IPaymentGatewaySettingsService _service;

    public TestPaymentGatewayEndpoint(IPaymentGatewaySettingsService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("/admin/settings/payment-gateways/test");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Test payment gateway credentials";
            s.Description = "Performs a non-money-movement credential check for a payment gateway.";
            s.Response(200, "Test result");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Settings"));
    }

    public override async Task HandleAsync(TestPaymentGatewayRequest req, CancellationToken ct)
    {
        var result = await _service.TestAsync(req.ProviderCode, ct);
        await Send.OkAsync(new TestPaymentGatewayResponse(
            result.Succeeded,
            result.ProviderCode,
            result.ErrorMessage), ct);
    }
}
