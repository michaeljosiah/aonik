using System.Net;
using System.Text;

using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Moq;

using Aonik.Commerce.Contracts.Models.Fulfilment;
using Aonik.Commerce.Services.Fulfilment;
using Aonik.Infrastructure.ExternalServices.Fulfilment;

namespace Aonik.Infrastructure.Tests.ExternalServices;

public sealed class GovUkBankHolidaySourceTests
{
    private const string ValidResponse = """
        {
          "england-and-wales":{"division":"england-and-wales","events":[
            {"title":"Christmas Day","date":"2026-12-25","notes":"","bunting":true},
            {"title":"New Year’s Day","date":"2026-01-01","notes":"","bunting":true}]},
          "scotland":{"division":"scotland","events":[
            {"title":"2nd January","date":"2026-01-02","notes":"","bunting":true}]},
          "northern-ireland":{"division":"northern-ireland","events":[
            {"title":"St Patrick’s Day","date":"2026-03-17","notes":"","bunting":true}]}
        }
        """;

    [Fact]
    public void Registration_Should_BoundRequestsAndDisableRedirectsAndRetries()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.ConfigureHttpClientDefaults(client => client.AddStandardResilienceHandler());
        services.AddInfrastructure(new ConfigurationBuilder().Build(),
            Mock.Of<IHostEnvironment>(environment => environment.EnvironmentName == "Testing"));
        using var provider = services.BuildServiceProvider();
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(IBankHolidaySource));
        var handler = provider.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(nameof(IBankHolidaySource));

        provider.GetRequiredService<IBankHolidaySource>().Should().BeOfType<GovUkBankHolidaySource>();
        client.Timeout.Should().Be(TimeSpan.FromSeconds(5));
        client.MaxResponseContentBufferSize.Should().Be(256 * 1024);
        while (handler is DelegatingHandler delegating)
        {
            handler.Should().NotBeOfType<ResilienceHandler>();
            handler = delegating.InnerHandler!;
        }
        handler.Should().BeOfType<HttpClientHandler>().Which.AllowAutoRedirect.Should().BeFalse();
    }

    [Theory]
    [InlineData("england-and-wales", "2026-01-01", "New Year’s Day", 2)]
    [InlineData("scotland", "2026-01-02", "2nd January", 1)]
    [InlineData("northern-ireland", "2026-03-17", "St Patrick’s Day", 1)]
    public async Task GetAsync_Should_ReadOnlyTheSelectedRegionFromTheFixedOfficialUrl(
        string region, string firstDate, string firstName, int count)
    {
        using var handler = Respond(HttpStatusCode.OK, ValidResponse);
        using var client = CreateClient(handler);
        client.BaseAddress = new Uri("https://untrusted.example/");

        var result = await new GovUkBankHolidaySource(client).GetAsync(region);

        result.Should().NotBeNull();
        result!.Region.Should().Be(region);
        result.Source.Should().Be("https://www.gov.uk/bank-holidays.json");
        result.Holidays.Should().HaveCount(count).And.BeInAscendingOrder(holiday => holiday.Date);
        result.Holidays[0].Should().Be(new BankHolidayDto(DateOnly.Parse(firstDate), firstName));
        handler.RequestUri.Should().Be(new Uri("https://www.gov.uk/bank-holidays.json"));
        handler.Method.Should().Be(HttpMethod.Get);
        handler.RequestCount.Should().Be(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData("wales")]
    [InlineData("Scotland")]
    [InlineData("https://untrusted.example/")]
    public async Task GetAsync_Should_RejectUnsupportedRegionsBeforeHttp(string region)
    {
        using var handler = new ScriptedHandler((_, _) => throw new InvalidOperationException("No HTTP expected."));
        using var client = CreateClient(handler);

        var act = () => new GovUkBankHolidaySource(client).GetAsync(region);

        await act.Should().ThrowAsync<ArgumentException>();
        handler.RequestCount.Should().Be(0);
    }

    [Theory]
    [InlineData(HttpStatusCode.MovedPermanently)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task GetAsync_Should_ReportHttpFailuresAsUnavailable(HttpStatusCode status)
    {
        using var handler = Respond(status, ValidResponse);
        using var client = CreateClient(handler);

        var result = await new GovUkBankHolidaySource(client).GetAsync("england-and-wales");

        result.Should().BeNull();
        handler.RequestCount.Should().Be(1);
    }

    [Theory]
    [InlineData("not JSON")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"england-and-wales\":null}")]
    [InlineData("{\"england-and-wales\":{\"division\":\"scotland\",\"events\":[{\"date\":\"2026-01-01\",\"title\":\"New Year\"}]}}")]
    [InlineData("{\"england-and-wales\":{\"division\":\"england-and-wales\",\"events\":[]}}")]
    [InlineData("{\"england-and-wales\":{\"division\":\"england-and-wales\",\"events\":[null]}}")]
    [InlineData("{\"england-and-wales\":{\"division\":\"england-and-wales\",\"events\":[{\"date\":\"2026-02-30\",\"title\":\"Holiday\"}]}}")]
    [InlineData("{\"england-and-wales\":{\"division\":\"england-and-wales\",\"events\":[{\"date\":\"2026-01-01\",\"title\":\" \"}]}}")]
    [InlineData("{\"england-and-wales\":{\"division\":\"england-and-wales\",\"events\":[{\"date\":\"2026-01-01\",\"title\":12}]}}")]
    public async Task GetAsync_Should_WithholdInvalidOrEmptySourceData(string body)
    {
        using var handler = Respond(HttpStatusCode.OK, body);
        using var client = CreateClient(handler);

        var result = await new GovUkBankHolidaySource(client).GetAsync("england-and-wales");

        result.Should().BeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetAsync_Should_BoundActualResponseBytes(bool knownLength)
    {
        var body = ValidResponse + new string(' ', checked((int)GovUkBankHolidaySource.MaxResponseBytes));
        using var handler = new ScriptedHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = knownLength ? new StringContent(body) : new UnknownLengthContent(body)
        }));
        using var client = CreateClient(handler);

        var result = await new GovUkBankHolidaySource(client).GetAsync("england-and-wales");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_Should_ReportNetworkFailureAsUnavailable()
    {
        using var handler = new ScriptedHandler((_, _) => throw new HttpRequestException("Connection failed."));
        using var client = CreateClient(handler);

        var result = await new GovUkBankHolidaySource(client).GetAsync("england-and-wales");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_Should_IncludeTheBodyInTheTimeout()
    {
        using var handler = new ScriptedHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new PendingContent()
        }));
        using var client = CreateClient(handler);
        client.Timeout = TimeSpan.FromMilliseconds(50);

        var result = await new GovUkBankHolidaySource(client).GetAsync("england-and-wales");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_Should_PropagateCallerCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        using var handler = new ScriptedHandler(async (_, ct) =>
        {
            cancellation.Cancel();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var client = CreateClient(handler);

        var act = () => new GovUkBankHolidaySource(client).GetAsync("england-and-wales", cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static HttpClient CreateClient(HttpMessageHandler handler) => new(handler)
    {
        Timeout = GovUkBankHolidaySource.RequestTimeout,
        MaxResponseContentBufferSize = GovUkBankHolidaySource.MaxResponseBytes
    };

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
            throw new InvalidOperationException("The response buffer must pass its cancellation token.");

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken) =>
            Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
