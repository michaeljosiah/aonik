using Aonik.Platform.Contracts.Api.Operations;
using Aonik.Platform.Contracts.Services.Operations;
using Aonik.Platform.Services.Operations;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Aonik.Platform.Endpoints.Operations.Alerts;

internal sealed class ReceiveAzureMonitorAlertEndpoint : Endpoint<AzureMonitorAlertWebhookRequest, AlertWebhookAcceptedResponse>
{
    private const string SharedSecretHeaderName = "X-Aonik-Integration-Key";

    private readonly IAlertIngestionService _alertIngestionService;
    private readonly ITenantContext _tenantContext;
    private readonly AzureMonitorAlertOptions _options;

    public ReceiveAzureMonitorAlertEndpoint(
        IAlertIngestionService alertIngestionService,
        ITenantContext tenantContext,
        IOptions<AzureMonitorAlertOptions> options)
    {
        _alertIngestionService = alertIngestionService;
        _tenantContext = tenantContext;
        _options = options.Value;
    }

    public override void Configure()
    {
        Post("/integrations/azure/alerts");
        AllowAnonymous();
    }

    public override async Task HandleAsync(AzureMonitorAlertWebhookRequest req, CancellationToken ct)
    {
        if (!HasValidSharedSecret())
        {
            HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "Invalid integration secret." }, ct);
            return;
        }

        try
        {
            _tenantContext.TenantId = Guid.Empty;
            _tenantContext.ResolutionSource = "AzureMonitorWebhook";

            var accepted = await _alertIngestionService.IngestAzureMonitorAlertAsync(req, ct);
            HttpContext.Response.StatusCode = StatusCodes.Status202Accepted;
            await HttpContext.Response.WriteAsJsonAsync(accepted, ct);
        }
        catch (ArgumentException ex)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new { error = ex.Message }, ct);
        }
    }

    private bool HasValidSharedSecret()
    {
        var configuredSecret = _options.SharedSecret?.Trim();
        if (string.IsNullOrWhiteSpace(configuredSecret))
        {
            return false;
        }

        var headerSecret = HttpContext.Request.Headers[SharedSecretHeaderName].FirstOrDefault()?.Trim();
        if (string.Equals(headerSecret, configuredSecret, StringComparison.Ordinal))
        {
            return true;
        }

        var querySecret = HttpContext.Request.Query["code"].FirstOrDefault()?.Trim();
        return string.Equals(querySecret, configuredSecret, StringComparison.Ordinal);
    }
}
