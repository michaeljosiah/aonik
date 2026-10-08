using System.Net;
using System.Text;

using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Moq;

using Aonik.Commerce.Contracts.Models.Fulfilment;
using Aonik.Commerce.Services.Fulfilment;
using Aonik.Infrastructure.ExternalServices.Postcodes;

namespace Aonik.Infrastructure.Tests.ExternalServices;

public class PostcodesIoLookupTests
{
    private const string ValidResponse = """
        {"status":200,"result":{"postcode":"SW1A 2AA","outcode":"SW1A","incode":"2AA","country":"England"}}
        """;

    [Fact]
    public async Task Registration_Should_DefaultToDisabledAndConfigureBoundedRequestsWithoutRedirectsOrRetries()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.ConfigureHttpClientDefaults(client => client.AddStandardResilienceHandler());
        services.AddInfrastructure(new ConfigurationBuilder().Build(),
            Mock.Of<IHostEnvironment>(environment => environment.EnvironmentName == "Testing"));
        using var provider = services.BuildServiceProvider();
        var clientFactory = provider.GetRequiredService<IHttpClientFactory>();
        using var client = clientFactory.CreateClient(nameof(IPostcodeLookup));
        var handler = provider.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(nameof(IPostcodeLookup));
        var lookup = provider.GetRequiredService<IPostcodeLookup>();

        var result = await lookup.LookupAsync("SW1A 2AA");

        result.Should().Be(new PostcodeLookupResult(PostcodeLookupStatus.Unavailable));
        client.Timeout.Should().Be(TimeSpan.FromSeconds(5));
        client.MaxResponseContentBufferSize.Should().Be(64 * 1024);
        while (handler is DelegatingHandler delegating)
        {
            handler.Should().NotBeOfType<ResilienceHandler>();
            handler = delegating.InnerHandler!;
        }
        handler.Should().BeOfType<HttpClientHandler>().Which.AllowAutoRedirect.Should().BeFalse();
    }

    [Fact]
    public async Task LookupAsync_Should_RemainUnavailableWithoutDeploymentOptIn()
    {
        using var handler = new ScriptedHandler((_, _) => throw new InvalidOperationException("No HTTP expected."));
        using var client = CreateClient(handler);
        var lookup = new PostcodesIoLookup(client, Options.Create(new PostcodesIoOptions()));

        var result = await lookup.LookupAsync("SW1A 2AA");

        result.Should().Be(new PostcodeLookupResult(PostcodeLookupStatus.Unavailable));
        handler.RequestCount.Should().Be(0);
    }

    [Theory]
    [InlineData("SW1A 2AA")]
    [InlineData("sw1a2aa")]
    public async Task LookupAsync_Should_VerifyTheRequestedPostcodeAtTheFixedHost(string postcode)
    {
        using var handler = Respond(HttpStatusCode.OK, ValidResponse);
        using var client = CreateClient(handler);
        client.BaseAddress = new Uri("https://untrusted.example/");
        var lookup = Enabled(client);

        var result = await lookup.LookupAsync(postcode);

        result.Should().Be(new PostcodeLookupResult(PostcodeLookupStatus.Found, "SW1A 2AA"));
        handler.RequestUri.Should().Be(new Uri("https://api.postcodes.io/postcodes/SW1A2AA"));
        handler.Method.Should().Be(HttpMethod.Get);
        handler.RequestCount.Should().Be(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://untrusted.example/")]
    [InlineData("SW1A 2AA/../other")]
    public async Task LookupAsync_Should_RejectMalformedInputBeforeHttp(string postcode)
    {
        using var handler = new ScriptedHandler((_, _) => throw new InvalidOperationException("No HTTP expected."));
        using var client = CreateClient(handler);

        var result = await Enabled(client).LookupAsync(postcode);

        result.Should().Be(new PostcodeLookupResult(PostcodeLookupStatus.NotFound));
        handler.RequestCount.Should().Be(0);
    }

    [Fact]
    public async Task LookupAsync_Should_KeepMissingCurrentPostcodesDistinctFromProviderFailure()
    {
        using var handler = Respond(HttpStatusCode.NotFound, """{"status":404,"error":"Invalid postcode"}""");
        using var client = CreateClient(handler);

        var result = await Enabled(client).LookupAsync("SW1A 2AA");

        result.Should().Be(new PostcodeLookupResult(PostcodeLookupStatus.NotFound));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.MovedPermanently)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task LookupAsync_Should_ReportOtherHttpFailuresAsUnavailable(HttpStatusCode status)
    {
        using var handler = Respond(status, ValidResponse);
        using var client = CreateClient(handler);

        var result = await Enabled(client).LookupAsync("SW1A 2AA");

        result.Should().Be(new PostcodeLookupResult(PostcodeLookupStatus.Unavailable));
        handler.RequestCount.Should().Be(1);
    }

    [Theory]
    [InlineData("not JSON")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"status\":200,\"result\":null}")]
    [InlineData("{\"status\":404,\"result\":{\"postcode\":\"SW1A 2AA\",\"outcode\":\"SW1A\"}}")]
    [InlineData("{\"status\":200,\"result\":{\"postcode\":\"SW1A 1AA\",\"outcode\":\"SW1A\"}}")]
    [InlineData("{\"status\":200,\"result\":{\"postcode\":\"SW1A 2AA\",\"outcode\":\"SW1\"}}")]
    [InlineData("{\"status\":200,\"result\":{\"postcode\":\"SW1A 2AA\"}}")]
    [InlineData("{\"status\":200,\"result\":{\"postcode\":123,\"outcode\":\"SW1A\"}}")]
    public async Task LookupAsync_Should_NotTrustMalformedOrMismatchedProviderResults(string body)
    {
        using var handler = Respond(HttpStatusCode.OK, body);
        using var client = CreateClient(handler);

        var result = await Enabled(client).LookupAsync("SW1A 2AA");

        result.Should().Be(new PostcodeLookupResult(PostcodeLookupStatus.Unavailable));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LookupAsync_Should_BoundResponseBytesWithOrWithoutContentLength(bool knownLength)
    {
        var body = ValidResponse + new string(' ', checked((int)PostcodesIoLookup.MaxResponseBytes));
        using var handler = new ScriptedHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = knownLength ? new StringContent(body) : new UnknownLengthContent(body)
        }));
        using var client = CreateClient(handler);

        var result = await Enabled(client).LookupAsync("SW1A 2AA");

        result.Should().Be(new PostcodeLookupResult(PostcodeLookupStatus.Unavailable));
    }

    [Fact]
    public async Task LookupAsync_Should_ReportNetworkFailureAsUnavailable()
    {
        using var handler = new ScriptedHandler((_, _) => throw new HttpRequestException("Connection failed."));
        using var client = CreateClient(handler);

        var result = await Enabled(client).LookupAsync("SW1A 2AA");

        result.Should().Be(new PostcodeLookupResult(PostcodeLookupStatus.Unavailable));
    }

    [Fact]
    public async Task LookupAsync_Should_ReportTimeoutAsUnavailable()
    {
        using var handler = new ScriptedHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var client = CreateClient(handler);
        client.Timeout = TimeSpan.FromMilliseconds(50);

        var result = await Enabled(client).LookupAsync("SW1A 2AA");

        result.Should().Be(new PostcodeLookupResult(PostcodeLookupStatus.Unavailable));
    }

    [Fact]
    public async Task LookupAsync_Should_IncludeTheResponseBodyInTheTimeout()
    {
        using var handler = new ScriptedHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new PendingContent()
        }));
        using var client = CreateClient(handler);
        client.Timeout = TimeSpan.FromMilliseconds(50);

        var result = await Enabled(client).LookupAsync("SW1A 2AA");

        result.Should().Be(new PostcodeLookupResult(PostcodeLookupStatus.Unavailable));
    }

    [Fact]
    public async Task LookupAsync_Should_PropagateCallerCancellationDuringHttp()
    {
        using var cancellation = new CancellationTokenSource();
        using var handler = new ScriptedHandler(async (_, ct) =>
        {
            cancellation.Cancel();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var client = CreateClient(handler);

        var act = () => Enabled(client).LookupAsync("SW1A 2AA", cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task LookupAsync_Should_HonorCancellationEvenWhenDisabled()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var handler = new ScriptedHandler((_, _) => throw new InvalidOperationException("No HTTP expected."));
        using var client = CreateClient(handler);
        var lookup = new PostcodesIoLookup(client, Options.Create(new PostcodesIoOptions()));

        var act = () => lookup.LookupAsync("SW1A 2AA", cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        handler.RequestCount.Should().Be(0);
    }

    private static HttpClient CreateClient(HttpMessageHandler handler) => new(handler)
    {
        Timeout = PostcodesIoLookup.RequestTimeout,
        MaxResponseContentBufferSize = PostcodesIoLookup.MaxResponseBytes
    };

    private static PostcodesIoLookup Enabled(HttpClient client) =>
        new(client, Options.Create(new PostcodesIoOptions { Enabled = true }));

    private static ScriptedHandler Respond(HttpStatusCode status, string body) => new((_, _) =>
        Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") }));

    private sealed class ScriptedHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public Uri? RequestUri { get; private set; }
        public HttpMethod? Method { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            RequestUri = request.RequestUri;
            Method = request.Method;
            return respond(request, cancellationToken);
        }
    }

    private sealed class UnknownLengthContent(string body) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            stream.WriteAsync(Encoding.UTF8.GetBytes(body)).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    private sealed class PendingContent : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            throw new InvalidOperationException("The client must pass its response-buffer cancellation token.");

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken) =>
            Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
