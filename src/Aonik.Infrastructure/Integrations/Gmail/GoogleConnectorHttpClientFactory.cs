using Google.Apis.Http;

namespace Aonik.Infrastructure.Integrations.Gmail;

internal sealed class GoogleConnectorHttpClientFactory(IHttpMessageHandlerFactory handlers) : Google.Apis.Http.HttpClientFactory
{
    protected override HttpMessageHandler CreateHandler(CreateHttpClientArgs args) => handlers.CreateHandler("GmailOAuth");
}
