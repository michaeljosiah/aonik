using Aonik.PersonalFinance.Agents.CodeAct;
using Aonik.SharedKernel.Abstractions.Agents;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Application.Tests.Agents.CodeAct;

public class CodeActCallbackNonceServiceTests
{
    private const string TestSigningKey = "0102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f20"; // 32 bytes hex

    private static CodeActCallbackNonceService CreateService(
        FakeTimeProvider? time = null,
        string? signingKey = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ai:CodeAct:NonceSigningKey"] = signingKey ?? TestSigningKey,
            })
            .Build();
        return new CodeActCallbackNonceService(
            config,
            NullLogger<CodeActCallbackNonceService>.Instance,
            time ?? new FakeTimeProvider());
    }

    private static CodeActSandboxContext CreateContext() => new(
        SubAgentName: "pf-insights",
        RunId: "run123",
        TenantId: Guid.NewGuid(),
        CurrentUserId: Guid.NewGuid());

    [Fact]
    public void Issue_Should_ProduceVerifiableNonce_When_Called()
    {
        var svc = CreateService();
        var ctx = CreateContext();

        var nonce = svc.Issue(ctx, new HashSet<string> { "pf_get_dashboard" }, 30, TimeSpan.FromSeconds(60));

        nonce.Should().NotBeNullOrWhiteSpace();
        svc.TryValidate(nonce, out var payload).Should().BeTrue();
        payload.Should().NotBeNull();
        payload!.SubAgentName.Should().Be("pf-insights");
        payload.RunId.Should().Be("run123");
        payload.TenantId.Should().Be(ctx.TenantId);
        payload.UserId.Should().Be(ctx.CurrentUserId);
        payload.ToolWhitelist.Should().BeEquivalentTo("pf_get_dashboard");
        payload.Jti.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void TryValidate_Should_RejectTamperedSignature_When_AnyByteFlipped()
    {
        var svc = CreateService();
        var nonce = svc.Issue(CreateContext(), new HashSet<string> { "pf_get_dashboard" }, 30, TimeSpan.FromMinutes(10));

        // Flip a single character in the signature (last segment).
        var parts = nonce.Split('.');
        var tampered = $"{parts[0]}.{parts[1]}.{(parts[2][0] == 'a' ? 'b' : 'a')}{parts[2][1..]}";

        svc.TryValidate(tampered, out var payload).Should().BeFalse();
        payload.Should().BeNull();
    }

    [Fact]
    public void TryValidate_Should_RejectTamperedPayload_When_BodyEdited()
    {
        var svc = CreateService();
        var nonce = svc.Issue(CreateContext(), new HashSet<string> { "pf_get_dashboard" }, 30, TimeSpan.FromMinutes(10));

        // Decode payload, edit, re-encode, re-attach original signature
        // → signature won't match.
        var parts = nonce.Split('.');
        var paddedPayload = parts[1] + new string('=', (4 - parts[1].Length % 4) % 4);
        paddedPayload = paddedPayload.Replace('-', '+').Replace('_', '/');
        var raw = Convert.FromBase64String(paddedPayload);
        // Mutate one byte.
        raw[10] ^= 0xff;
        var rePayload = Convert.ToBase64String(raw).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var tampered = $"{parts[0]}.{rePayload}.{parts[2]}";

        svc.TryValidate(tampered, out _).Should().BeFalse();
    }

    [Fact]
    public void TryValidate_Should_RejectExpiredNonce_When_PastExpiry()
    {
        var time = new FakeTimeProvider { UtcNow = DateTimeOffset.UtcNow };
        var svc = CreateService(time);
        var nonce = svc.Issue(CreateContext(), new HashSet<string> { "pf_get_dashboard" }, 30, TimeSpan.FromSeconds(10));

        time.UtcNow = time.UtcNow.AddSeconds(11);

        svc.TryValidate(nonce, out _).Should().BeFalse();
    }

    [Fact]
    public void TryValidate_Should_RejectMissingVersionHeader_When_PrefixMissing()
    {
        var svc = CreateService();
        var nonce = svc.Issue(CreateContext(), new HashSet<string> { "pf_get_dashboard" }, 30, TimeSpan.FromMinutes(10));

        var parts = nonce.Split('.');
        var bad = $"badversion.{parts[1]}.{parts[2]}";

        svc.TryValidate(bad, out _).Should().BeFalse();
    }

    [Fact]
    public void TryValidate_Should_RejectMalformedToken_When_NotThreeSegments()
    {
        var svc = CreateService();
        svc.TryValidate("nonce_v1.justonepart", out _).Should().BeFalse();
        svc.TryValidate("totally-not-a-nonce", out _).Should().BeFalse();
        svc.TryValidate("", out _).Should().BeFalse();
    }

    [Fact]
    public void TryConsumeBudget_Should_DecrementThenReject_When_Exhausted()
    {
        var svc = CreateService();
        var nonce = svc.Issue(CreateContext(), new HashSet<string> { "x" }, maxCallbacks: 3, TimeSpan.FromMinutes(10));
        svc.TryValidate(nonce, out var payload).Should().BeTrue();

        svc.TryConsumeBudget(payload!.Jti).Should().BeTrue();   // 3 → 2
        svc.TryConsumeBudget(payload.Jti).Should().BeTrue();    // 2 → 1
        svc.TryConsumeBudget(payload.Jti).Should().BeTrue();    // 1 → 0
        svc.TryConsumeBudget(payload.Jti).Should().BeFalse();   // exhausted
        svc.PeekBudget(payload.Jti).Should().Be(0);
    }

    [Fact]
    public void TryConsumeBudget_Should_ReturnFalse_When_NonceUnknown()
    {
        var svc = CreateService();
        svc.TryConsumeBudget("unknown-jti").Should().BeFalse();
    }

    [Fact]
    public async Task TryConsumeBudget_Should_BeAtomicUnderConcurrency_When_RacingThreads()
    {
        var svc = CreateService();
        var nonce = svc.Issue(CreateContext(), new HashSet<string> { "x" }, maxCallbacks: 10, TimeSpan.FromMinutes(10));
        svc.TryValidate(nonce, out var payload).Should().BeTrue();

        // 50 racing decrements; exactly 10 must succeed.
        var tasks = Enumerable.Range(0, 50)
            .Select(_ => Task.Run(() => svc.TryConsumeBudget(payload!.Jti)))
            .ToArray();
        var results = await Task.WhenAll(tasks);

        results.Count(r => r).Should().Be(10);
        svc.PeekBudget(payload!.Jti).Should().Be(0);
    }

    [Fact]
    public void Issue_Should_Throw_When_KeyMissing()
    {
        // Key resolution is lazy (deferred from the ctor) because FastEndpoints
        // constructs every endpoint — and transitively this service — at host
        // start. Throwing at first-use keeps the API bootable in environments
        // that don't enable the AcaSessions provider.
        var config = new ConfigurationBuilder().Build();
        var svc = new CodeActCallbackNonceService(config, NullLogger<CodeActCallbackNonceService>.Instance);
        var act = () => svc.Issue(CreateContext(), new HashSet<string> { "x" }, 30, TimeSpan.FromSeconds(60));
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Ai:CodeAct:NonceSigningKey*");
    }

    [Fact]
    public void Issue_Should_Throw_When_KeyTooShort()
    {
        var shortKey = "00112233"; // 4 bytes hex — way under 32-byte minimum
        var svc = CreateService(signingKey: shortKey);
        var act = () => svc.Issue(CreateContext(), new HashSet<string> { "x" }, 30, TimeSpan.FromSeconds(60));
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*at least 32 bytes*");
    }

    [Fact]
    public void Issue_Should_ProduceDistinctJti_When_CalledRepeatedly()
    {
        var svc = CreateService();
        var ctx = CreateContext();
        var allowed = new HashSet<string> { "x" };

        var jtis = new HashSet<string>();
        for (var i = 0; i < 50; i++)
        {
            var n = svc.Issue(ctx, allowed, 30, TimeSpan.FromMinutes(10));
            svc.TryValidate(n, out var p).Should().BeTrue();
            jtis.Add(p!.Jti);
        }
        jtis.Should().HaveCount(50, "each Issue call must mint a fresh jti");
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
