using System.Text;
using Aonik.Infrastructure.ExternalServices.Plaid;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Infrastructure.Tests.ExternalServices.Plaid;

/// <summary>
/// Covers the wiring seam of the Plaid webhook verification middleware (issue H13): path
/// gating, the simulation pass-through, rejection on failed verification, and body rewind.
/// The crypto itself is exercised in <see cref="PlaidWebhookVerifierTests"/>.
/// </summary>
public class PlaidWebhookVerificationMiddlewareTests
{
    private const string WebhookPath = "/personal-finance/account-links/webhooks/plaid";

    private sealed class FakeVerifier(bool enabled, bool valid) : IPlaidWebhookVerifier
    {
        public bool IsEnabled { get; } = enabled;
        public bool WasCalled { get; private set; }
        public byte[]? SeenBody { get; private set; }

        public Task<bool> VerifyAsync(string? verificationHeader, ReadOnlyMemory<byte> rawBody, CancellationToken cancellationToken)
        {
            WasCalled = true;
            SeenBody = rawBody.ToArray();
            return Task.FromResult(valid);
        }
    }

    private static DefaultHttpContext MakeContext(string path, byte[] body, string method = "POST", long? contentLength = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.Body = new MemoryStream(body);
        context.Request.ContentLength = contentLength ?? body.Length;
        context.Request.Headers["Plaid-Verification"] = "header.value.sig";
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<bool> InvokeAsync(FakeVerifier verifier, DefaultHttpContext context)
    {
        var nextCalled = false;
        var middleware = new PlaidWebhookVerificationMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, verifier, NullLogger<PlaidWebhookVerificationMiddleware>.Instance);
        return nextCalled;
    }

    [Fact]
    public async Task Should_PassThrough_When_PathIsNotAPlaidWebhook()
    {
        var verifier = new FakeVerifier(enabled: true, valid: false);
        var context = MakeContext("/some/other/path", Encoding.UTF8.GetBytes("{}"));

        var nextCalled = await InvokeAsync(verifier, context);

        nextCalled.Should().BeTrue();
        verifier.WasCalled.Should().BeFalse("non-webhook paths must not be verified");
    }

    [Fact]
    public async Task Should_PassThrough_When_VerificationDisabled()
    {
        var verifier = new FakeVerifier(enabled: false, valid: false);
        var context = MakeContext(WebhookPath, Encoding.UTF8.GetBytes("{}"));

        var nextCalled = await InvokeAsync(verifier, context);

        nextCalled.Should().BeTrue();
        verifier.WasCalled.Should().BeFalse("simulation mode has no signature to verify");
    }

    [Fact]
    public async Task Should_Return401_When_VerificationFails()
    {
        var verifier = new FakeVerifier(enabled: true, valid: false);
        var context = MakeContext(WebhookPath, Encoding.UTF8.GetBytes("{}"));

        var nextCalled = await InvokeAsync(verifier, context);

        nextCalled.Should().BeFalse("a failed webhook must never reach the processor");
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task Should_CallNext_AndLeaveBodyReadable_When_VerificationSucceeds()
    {
        var body = Encoding.UTF8.GetBytes("""{"webhook_type":"TRANSACTIONS"}""");
        var verifier = new FakeVerifier(enabled: true, valid: true);
        var context = MakeContext(WebhookPath, body);

        var nextCalled = await InvokeAsync(verifier, context);

        nextCalled.Should().BeTrue();
        verifier.SeenBody.Should().Equal(body, "the verifier must see the exact raw body");
        context.Request.Body.Position.Should().Be(0, "the body must be rewound so the endpoint can still bind it");
    }

    [Fact]
    public async Task Should_Return413_When_BodyExceedsCap()
    {
        var oversized = new byte[(256 * 1024) + 1];
        var verifier = new FakeVerifier(enabled: true, valid: true);
        var context = MakeContext(WebhookPath, oversized);

        var nextCalled = await InvokeAsync(verifier, context);

        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status413PayloadTooLarge);
        verifier.WasCalled.Should().BeFalse();
    }
}
