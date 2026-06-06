using Aonik.Agents.Framework;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Aonik.Application.Tests.Agents.Tenant;

/// <summary>
/// Spec 033 §11 — the egress allow-list is the SSRF guard. These cover host matching (exact +
/// wildcard), the https requirement, and the fail-closed default for unlisted hosts.
/// </summary>
public sealed class TenantEgressAllowListTests
{
    private sealed class StaticMonitor<T> : IOptionsMonitor<T>
    {
        private readonly T _value;
        public StaticMonitor(T value) => _value = value;
        public T CurrentValue => _value;
        public T Get(string? name) => _value;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private static TenantEgressAllowList Build(TenantExtensionOptions options) =>
        new(new StaticMonitor<TenantExtensionOptions>(options));

    [Fact]
    public void IsAllowed_Should_Permit_ExactHost_OverHttps()
    {
        var sut = Build(new TenantExtensionOptions { AllowedEgressHosts = { "api.example.com" } });

        sut.IsAllowed("https://api.example.com/v1/tools", out var reason).Should().BeTrue();
        reason.Should().BeNull();
    }

    [Fact]
    public void IsAllowed_Should_Reject_UnlistedHost()
    {
        var sut = Build(new TenantExtensionOptions { AllowedEgressHosts = { "api.example.com" } });

        sut.IsAllowed("https://evil.internal/secrets", out var reason).Should().BeFalse();
        reason.Should().Contain("allow-list");
    }

    [Fact]
    public void IsAllowed_Should_Reject_Http_When_InsecureNotAllowed()
    {
        var sut = Build(new TenantExtensionOptions { AllowedEgressHosts = { "api.example.com" } });

        sut.IsAllowed("http://api.example.com/v1", out var reason).Should().BeFalse();
        reason.Should().Contain("https");
    }

    [Fact]
    public void IsAllowed_Should_Match_WildcardSubdomain_ButNotApex()
    {
        var sut = Build(new TenantExtensionOptions { AllowedEgressHosts = { "*.example.com" } });

        sut.IsAllowed("https://tools.example.com/x", out _).Should().BeTrue();
        sut.IsAllowed("https://example.com/x", out _).Should().BeFalse("the wildcard matches sub-domains, not the apex");
    }

    [Fact]
    public void IsAllowed_Should_Reject_MalformedUrl()
    {
        var sut = Build(new TenantExtensionOptions { AllowAnyEgressHost = true });

        sut.IsAllowed("not-a-url", out var reason).Should().BeFalse();
        reason.Should().NotBeNull();
    }

    [Fact]
    public void IsAllowed_Should_Permit_AnyHost_When_Configured()
    {
        var sut = Build(new TenantExtensionOptions { AllowAnyEgressHost = true });

        sut.IsAllowed("https://anything.test/x", out _).Should().BeTrue();
    }
}
