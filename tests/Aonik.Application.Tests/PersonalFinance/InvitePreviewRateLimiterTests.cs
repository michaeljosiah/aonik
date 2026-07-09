using Aonik.PersonalFinance.Services;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Aonik.Application.Tests.PersonalFinance;

/// <summary>Spec 061 §10: the anonymous invite-preview limiter trips independently per-IP and per-token.</summary>
public class InvitePreviewRateLimiterTests
{
    private static InvitePreviewRateLimiter Create(CircleInviteOptions options)
        => new(new MemoryCache(new MemoryCacheOptions()), Microsoft.Extensions.Options.Options.Create(options));

    [Fact]
    public void ShouldAllow_TripsPerIp_AfterCeiling()
    {
        var limiter = Create(new CircleInviteOptions
        {
            PreviewRateLimitPerIp = 3,
            PreviewRateLimitPerToken = 1000, // high, so only the IP ceiling can trip here
            PreviewRateLimitWindowSeconds = 60,
        });

        // Same IP, different tokens — the per-IP ceiling governs.
        limiter.ShouldAllow("1.2.3.4", "t1").Should().BeTrue();
        limiter.ShouldAllow("1.2.3.4", "t2").Should().BeTrue();
        limiter.ShouldAllow("1.2.3.4", "t3").Should().BeTrue();
        limiter.ShouldAllow("1.2.3.4", "t4").Should().BeFalse(); // 4th from this IP in the window is blocked

        limiter.ShouldAllow("9.9.9.9", "t5").Should().BeTrue(); // a different IP is unaffected
    }

    [Fact]
    public void ShouldAllow_TripsPerToken_AfterCeiling()
    {
        var limiter = Create(new CircleInviteOptions
        {
            PreviewRateLimitPerIp = 1000, // high, so only the token ceiling can trip here
            PreviewRateLimitPerToken = 2,
            PreviewRateLimitWindowSeconds = 60,
        });

        // Same token, different IPs — the per-token ceiling governs.
        limiter.ShouldAllow("1.1.1.1", "tok").Should().BeTrue();
        limiter.ShouldAllow("2.2.2.2", "tok").Should().BeTrue();
        limiter.ShouldAllow("3.3.3.3", "tok").Should().BeFalse(); // 3rd hit on this token is blocked

        limiter.ShouldAllow("4.4.4.4", "other").Should().BeTrue(); // a different token is unaffected
    }

    [Fact]
    public void ShouldAllow_NonPositiveCeiling_DisablesThatDimension()
    {
        var limiter = Create(new CircleInviteOptions { PreviewRateLimitPerIp = 0, PreviewRateLimitPerToken = 0 });

        for (var i = 0; i < 50; i++)
        {
            limiter.ShouldAllow("1.2.3.4", "tok").Should().BeTrue();
        }
    }
}
