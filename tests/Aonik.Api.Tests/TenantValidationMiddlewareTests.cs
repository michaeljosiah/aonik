using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

using Aonik.Api.Middleware;
using Aonik.Application.Abstractions.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Api.Tests;

// Guards the anonymous pre-tenant skip list in TenantValidationMiddleware. The
// IAonikDbContext is only touched once a tenant is resolved, so the skip and the
// unresolved-401 paths exercised here never dereference it (null! is safe). A real
// DbContext would only be needed to test the resolved-tenant status checks.
public class TenantValidationMiddlewareTests
{
    [Theory]
    [InlineData("/auth/token", "POST")]              // credential→token exchange (this fix)
    [InlineData("/v1/settings/auth-provider", "GET")] // provider discovery (prior fix)
    [InlineData("/v1/settings/public", "GET")]
    [InlineData("/health", "GET")]
    public async Task InvokeAsync_ShouldPassThrough_WhenPathIsPreTenantWhitelisted_AndTenantUnresolved(
        string path, string method)
    {
        // Arrange — no tenant resolvable (the ACA / no-subdomain CLI case)
        var nextCalled = false;
        var middleware = new TenantValidationMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            NullLogger<TenantValidationMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = method;
        var tenantContext = new FakeTenantContext(); // IsResolved == false

        // Act
        await middleware.InvokeAsync(context, dbContext: null!, tenantContext);

        // Assert — request reaches the endpoint instead of 401 "Tenant context missing"
        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Theory]
    [InlineData("/orders", "GET")]
    [InlineData("/auth/tokens-for-something-else", "POST")] // not the exact /auth/token segment
    public async Task InvokeAsync_ShouldReturn401_WhenPathIsTenantScoped_AndTenantUnresolved(
        string path, string method)
    {
        // Arrange — the skip must stay narrow: a tenant-scoped path with no tenant is still blocked
        var nextCalled = false;
        var middleware = new TenantValidationMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            NullLogger<TenantValidationMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Path = path;
        context.Request.Method = method;
        var tenantContext = new FakeTenantContext(); // IsResolved == false

        // Act
        await middleware.InvokeAsync(context, dbContext: null!, tenantContext);

        // Assert
        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    private sealed class FakeTenantContext : ITenantContext
    {
        public Guid? TenantId { get; set; }
        public string? ResolutionSource { get; set; }
        public bool IsResolved => TenantId.HasValue;
    }
}
