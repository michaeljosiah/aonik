using System.Net;

using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

using Aonik.Agents.Framework;
using Aonik.Agents.Services.Connectors.Catalog;
using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Application.Tests.Agents.Connectors;

public sealed class ConnectorPackageTransportTests
{
    [Fact]
    public async Task CuratedClient_Should_DispatchOnce_WhenTheHostHasAutomaticRetryDefaults()
    {
        var services = new ServiceCollection(); services.AddLogging();
        services.ConfigureHttpClientDefaults(client => client.AddStandardResilienceHandler(options => options.Retry.Delay = TimeSpan.FromMilliseconds(1)));
        Aonik.Agents.AgentsModule.ConfigureServices(services, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["UseInMemoryDatabase"] = "true" }).Build());
        var egress = new Mock<ITenantEgressAllowList>(); string? reason = null;
        egress.Setup(value => value.IsAllowed(It.IsAny<string>(), out reason)).Returns(true);
        services.AddSingleton(egress.Object);
        var remote = new Remote("transient");
        services.AddHttpClient("CuratedConnectorTool").ConfigurePrimaryHttpMessageHandler(() => remote);
        await using ServiceProvider provider = services.BuildServiceProvider();
        using HttpClient client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("CuratedConnectorTool");

        using HttpResponseMessage response = await client.PostAsync("https://reviewed.example.test/mcp", new StringContent("{}"));

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        remote.Requests.Should().Be(1, "the durable approval journal, not host HTTP retries, owns recovery of a remote write");
    }

    [Theory]
    [InlineData("egress", "EndpointBlocked")]
    [InlineData("redirect", "RedirectRefused")]
    [InlineData("oversized", "ResponseTooLarge")]
    public async Task Transport_Should_RefuseUnreviewedDestinationsAndOversizedProtocolReplies(string denied, string code)
    {
        var egress = new Mock<ITenantEgressAllowList>(); string? reason = null;
        egress.Setup(value => value.IsAllowed(It.IsAny<string>(), out reason)).Returns(denied != "egress");
        var remote = new Remote(denied);
        using var client = new HttpClient(new ConnectorPackageHttpHandler(egress.Object) { InnerHandler = remote });

        Func<Task> send = () => client.PostAsync("https://reviewed.example.test/mcp", new StringContent("{}"));

        if (denied == "egress")
        {
            await send.Should().ThrowAsync<ConnectorAccessException>().Where(error => error.Code == code);
            remote.Requests.Should().Be(0);
        }
        else
        {
            await send.Should().ThrowAsync<ConnectorProviderException>().Where(error => error.Code == code);
            remote.Requests.Should().Be(1);
        }
    }

    [Fact]
    public async Task Transport_Should_ReturnTheExactBoundedReplyWithoutChangingItsMediaType()
    {
        var egress = new Mock<ITenantEgressAllowList>(); string? reason = null;
        egress.Setup(value => value.IsAllowed(It.IsAny<string>(), out reason)).Returns(true);
        using var client = new HttpClient(new ConnectorPackageHttpHandler(egress.Object) { InnerHandler = new Remote("ready") });

        using HttpResponseMessage response = await client.PostAsync("https://reviewed.example.test/mcp", new StringContent("{}"));

        (await response.Content.ReadAsStringAsync()).Should().Be("{\"status\":\"ready\"}");
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    private sealed class Remote(string response) : HttpMessageHandler
    {
        public int Requests { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            return Task.FromResult(new HttpResponseMessage(response == "redirect" ? HttpStatusCode.TemporaryRedirect : response == "transient" ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK)
            {
                Content = response == "oversized" ? new ByteArrayContent(new byte[ConnectorPackageHttpHandler.MaximumProtocolBytes + 1])
                    : new StringContent("{\"status\":\"ready\"}", System.Text.Encoding.UTF8, "application/json")
            });
        }
    }
}
