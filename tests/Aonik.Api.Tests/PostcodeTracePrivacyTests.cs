using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry.Instrumentation.Http;
using OpenTelemetry.Trace;

namespace Aonik.Api.Tests;

public class PostcodeTracePrivacyTests
{
    [Fact]
    public void ConfigureOpenTelemetry_Should_ExcludeOnlyPostcodeLookupPathsFromHttpTraces()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            DisableDefaults = true,
            EnvironmentName = "Testing"
        });
        builder.ConfigureOpenTelemetry();
        using var host = builder.Build();
        // Materialize the same tracing registration used by API startup before reading its options.
        _ = host.Services.GetRequiredService<TracerProvider>();
        var options = host.Services.GetRequiredService<IOptions<HttpClientTraceInstrumentationOptions>>().Value;
        options.FilterHttpRequestMessage.Should().NotBeNull();

        using var lookup = new HttpRequestMessage(HttpMethod.Get, "https://api.postcodes.io/postcodes/SW1A2AA");
        using var ordinary = new HttpRequestMessage(HttpMethod.Get, "https://payments.example/payment-intents/123");
        using var otherHost = new HttpRequestMessage(HttpMethod.Get, "https://other.example/postcodes/SW1A2AA");
        using var similarHost = new HttpRequestMessage(HttpMethod.Get, "https://api.postcodes.io.example/postcodes/SW1A2AA");
        using var otherPath = new HttpRequestMessage(HttpMethod.Get, "https://api.postcodes.io/health");

        options.FilterHttpRequestMessage!(lookup).Should().BeFalse();
        options.FilterHttpRequestMessage(ordinary).Should().BeTrue();
        options.FilterHttpRequestMessage(otherHost).Should().BeTrue();
        options.FilterHttpRequestMessage(similarHost).Should().BeTrue();
        options.FilterHttpRequestMessage(otherPath).Should().BeTrue();
    }
}
