using System.Reflection;

using Aonik.Infrastructure.Authentication.Provisioning;
using FluentAssertions;

namespace Aonik.Infrastructure.Tests.Authentication;

/// <summary>
/// Spec 029 — covers the URL-manipulation helpers shared by every Keycloak*
/// service. <see cref="KeycloakUrls"/> is internal so tests reach it via
/// reflection rather than punching it into the public API; the surface is
/// small enough (two methods) that the indirection is the cheaper trade.
/// </summary>
public class KeycloakUrlsTests
{
    private static string InvokeNormalizeAuthority(string input) =>
        (string)typeof(KeycloakUrls)
            .GetMethod("NormalizeAuthority", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!
            .Invoke(null, [input])!;

    private static string InvokeRealmRoot(string input) =>
        (string)typeof(KeycloakUrls)
            .GetMethod("RealmRoot", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!
            .Invoke(null, [input])!;

    [Theory]
    [InlineData("https://keycloak.example.com/realms/aonik", "https://keycloak.example.com/realms/aonik")]
    [InlineData("https://keycloak.example.com/realms/aonik/", "https://keycloak.example.com/realms/aonik")]
    [InlineData("http://localhost:8080/realms/aonik", "http://localhost:8080/realms/aonik")]
    [InlineData("  https://keycloak.example.com/realms/aonik  ", "https://keycloak.example.com/realms/aonik")]
    [InlineData("keycloak.example.com/realms/aonik", "https://keycloak.example.com/realms/aonik")]
    public void NormalizeAuthority_Should_ReturnExpectedShape(string input, string expected)
    {
        InvokeNormalizeAuthority(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("https://keycloak.example.com/realms/aonik", "https://keycloak.example.com")]
    [InlineData("http://localhost:8080/realms/aonik", "http://localhost:8080")]
    [InlineData("https://keycloak.example.com/realms/aonik-prod", "https://keycloak.example.com")]
    public void RealmRoot_Should_StripRealmsSegment(string input, string expected)
    {
        InvokeRealmRoot(input).Should().Be(expected);
    }

    [Fact]
    public void RealmRoot_Should_ReturnInputUnchanged_When_NoRealmsSegment()
    {
        // A misconfigured authority surfaces as a clear 404 from the upstream
        // rather than a silent rewrite. The helper deliberately doesn't try
        // to be clever here.
        InvokeRealmRoot("https://keycloak.example.com").Should().Be("https://keycloak.example.com");
    }
}
