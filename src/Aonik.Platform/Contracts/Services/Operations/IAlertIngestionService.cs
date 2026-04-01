using Aonik.Platform.Contracts.Api.Operations;

namespace Aonik.Platform.Contracts.Services.Operations;

public interface IAlertIngestionService
{
    Task<AlertWebhookAcceptedResponse> IngestAzureMonitorAlertAsync(
        AzureMonitorAlertWebhookRequest request,
        CancellationToken cancellationToken = default);
}
