using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Aonik.Agents.Entities;
using Aonik.Agents.Framework;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Xunit;

namespace Aonik.Application.Tests.Agents.Tenant;

/// <summary>
/// Spec 033 §8.4/§11 — the declarative HTTP tool must not auto-follow redirects, or an allow-listed
/// endpoint could bounce the call to a non-allow-listed/internal host (SSRF) after the single egress
/// check and forward auth headers cross-host. The tool surfaces a 3xx instead of chasing it.
/// </summary>
public sealed class DeclarativeHttpToolRedirectTests
{
    private sealed class CountingHandler : HttpMessageHandler
    {
        public int Calls;
        public required Func<HttpResponseMessage> Respond;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Respond());
        }
    }

    private sealed class StubFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public StubFactory(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }

    private sealed class AllowAllEgress : ITenantEgressAllowList
    {
        public bool IsAllowed(string? url, out string? reason) { reason = null; return true; }
    }

    private sealed class NoopProtector : ITenantCredentialProtector
    {
        public string Protect(string plaintext) => plaintext;
        public string Unprotect(string protectedValue) => protectedValue;
    }

    private static TenantHttpTool GetTool() => new()
    {
        Name = "fetch_data",
        Method = "GET",
        UrlTemplate = "https://api.example.com/data",
        ParameterSchemaJson = "{}",
        AuthKind = TenantToolAuthKind.None,
    };

    private static DeclarativeHttpAIFunction Build(CountingHandler handler) =>
        new(GetTool(), new NoopProtector(), new AllowAllEgress(), new StubFactory(handler));

    [Fact]
    public async Task Redirect_IsNotFollowed_And_HandlerCalledOnce()
    {
        var handler = new CountingHandler
        {
            Respond = () =>
            {
                var r = new HttpResponseMessage(HttpStatusCode.Redirect);
                r.Headers.Location = new Uri("https://evil.internal/steal");
                return r;
            },
        };

        var result = await Build(handler).InvokeAsync(new AIFunctionArguments(), CancellationToken.None);

        result!.ToString().Should().Contain("not followed");
        handler.Calls.Should().Be(1, "the tool must surface the 30x, not chase it to the redirect target");
    }

    [Fact]
    public async Task SuccessResponse_ReturnsBody()
    {
        var handler = new CountingHandler
        {
            Respond = () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"ok\":true}") },
        };

        var result = await Build(handler).InvokeAsync(new AIFunctionArguments(), CancellationToken.None);

        result!.ToString().Should().Contain("ok");
        handler.Calls.Should().Be(1);
    }
}
