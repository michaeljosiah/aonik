using System.Security.Claims;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Infrastructure.Authentication;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Aonik.Infrastructure.Tests.Authentication;

/// <summary>
/// Covers the async tenant resolution introduced by H16 (the subdomain path is awaited rather
/// than blocked with .GetAwaiter().GetResult()). The non-DB modes are asserted here with a strict
/// DB mock so any accidental database touch fails the test.
/// </summary>
public class TenantResolverTests
{
    private static TenantResolver Create(HttpContext? httpContext, string routingMode, Mock<IAonikDbContext> db)
    {
        var accessor = Mock.Of<IHttpContextAccessor>(a => a.HttpContext == httpContext);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:TenantRouting"] = routingMode })
            .Build();
        return new TenantResolver(db.Object, config, accessor, NullLogger<TenantResolver>.Instance);
    }

    [Fact]
    public async Task ResolveTenantIdAsync_Should_ReturnHeaderTenant_WithoutTouchingTheDatabase()
    {
        var tenantId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Tenant-Id"] = tenantId.ToString();
        var db = new Mock<IAonikDbContext>(MockBehavior.Strict);

        var result = await Create(httpContext, "Header", db).ResolveTenantIdAsync();

        result.Should().Be(tenantId);
        db.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ResolveTenantIdAsync_Should_ReturnClaimTenant_WithoutTouchingTheDatabase()
    {
        var tenantId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("aonik_tenant_id", tenantId.ToString())])),
        };
        var db = new Mock<IAonikDbContext>(MockBehavior.Strict);

        var result = await Create(httpContext, "Claim", db).ResolveTenantIdAsync();

        result.Should().Be(tenantId);
        db.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ResolveTenantIdAsync_Should_ReturnNull_WhenNoHttpContext()
    {
        var db = new Mock<IAonikDbContext>(MockBehavior.Strict);

        var result = await Create(httpContext: null, "Header", db).ResolveTenantIdAsync();

        result.Should().BeNull();
        db.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ResolveTenantIdAsync_Should_ReturnNull_WhenHeaderMalformed()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Tenant-Id"] = "not-a-guid";
        var db = new Mock<IAonikDbContext>(MockBehavior.Strict);

        var result = await Create(httpContext, "Header", db).ResolveTenantIdAsync();

        result.Should().BeNull();
        db.VerifyNoOtherCalls();
    }
}
