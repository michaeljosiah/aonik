using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Services.Identity;
using Aonik.Domain.Identity.Entities;
using Aonik.Infrastructure.Persistence;

namespace Aonik.Application.Tests.Identity;

public class UserIdentityServiceTests
{
    private sealed class TestTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;

        public TestTenantProvider(Guid tenantId) => _tenantId = tenantId;

        public Guid GetCurrentTenantId() => _tenantId;

        public bool TryGetCurrentTenantId(out Guid tenantId)
        {
            tenantId = _tenantId;
            return true;
        }
    }

    [Fact]
    public async Task ResolveOrCreateUserAsync_ShouldCreateUserOnFirstLogin()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AonikDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        var tenantProvider = new TestTenantProvider(tenantId);
        using var context = new AonikDbContext(options, tenantProvider);
        context.Tenants.Add(new Tenant
        {
            TenantId = tenantId,
            Name = "Test Tenant",
            Environment = "Testing",
            DefaultCurrency = "USD",
            SupportedCountriesJson = "[]",
            Status = TenantStatus.Active
        });
        await context.SaveChangesAsync();

        var service = new UserIdentityService(context, NullLogger<UserIdentityService>.Instance);

        // Act
        var user = await service.ResolveOrCreateUserAsync(
            externalIssuer: "test-issuer",
            externalSubject: "test-subject",
            externalTenantId: "external-tenant",
            email: "first@login.test",
            aonikTenantId: tenantId,
            ct: CancellationToken.None);

        // Assert
        user.Should().NotBeNull();
        user.TenantId.Should().Be(tenantId);
        context.Users.Should().HaveCount(1);
    }

    [Fact]
    public async Task ResolveOrCreateUserAsync_ShouldReuseExistingUserOnRepeatLogin()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AonikDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        var tenantProvider = new TestTenantProvider(tenantId);
        using var context = new AonikDbContext(options, tenantProvider);
        context.Tenants.Add(new Tenant
        {
            TenantId = tenantId,
            Name = "Test Tenant",
            Environment = "Testing",
            DefaultCurrency = "USD",
            SupportedCountriesJson = "[]",
            Status = TenantStatus.Active
        });
        await context.SaveChangesAsync();

        var service = new UserIdentityService(context, NullLogger<UserIdentityService>.Instance);

        var firstUser = await service.ResolveOrCreateUserAsync(
            externalIssuer: "test-issuer",
            externalSubject: "test-subject",
            externalTenantId: "external-tenant",
            email: "first@login.test",
            aonikTenantId: tenantId,
            ct: CancellationToken.None);

        // Act
        var secondUser = await service.ResolveOrCreateUserAsync(
            externalIssuer: "test-issuer",
            externalSubject: "test-subject",
            externalTenantId: "external-tenant",
            email: "second@login.test",
            aonikTenantId: tenantId,
            ct: CancellationToken.None);

        // Assert
        secondUser.Id.Should().Be(firstUser.Id);
        context.Users.Should().HaveCount(1);
        secondUser.Email.Should().Be("second@login.test");
    }
}
