using System.Text.Json;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Observability;
using Aonik.Application.Services.Compliance;
using Aonik.Application.Services.Identity;
using Aonik.Platform.Entities.Identity;
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

    private sealed class TestAuditLogWriter : IAuditLogWriter
    {
        public AuditLogEntry? LastEntry { get; private set; }

        public Task LogAsync(
            string action,
            string resourceType,
            Guid resourceId,
            Guid tenantId,
            Guid? actorId,
            string? correlationId,
            string? detailsJson = null,
            CancellationToken cancellationToken = default)
        {
            LastEntry = new AuditLogEntry(action, resourceType, resourceId, tenantId, actorId, correlationId, detailsJson);
            return Task.CompletedTask;
        }
    }

    private sealed record AuditLogEntry(
        string Action,
        string ResourceType,
        Guid ResourceId,
        Guid TenantId,
        Guid? ActorId,
        string? CorrelationId,
        string? DetailsJson);

    private sealed class TestCorrelationContext : ICorrelationContext
    {
        public TestCorrelationContext(string? correlationId) => CorrelationId = correlationId;

        public string? CorrelationId { get; }
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
            Id = tenantId,
            Name = "Test Tenant",
            Environment = "Testing",
            DefaultCurrency = "USD",
            SupportedCountriesJson = "[]",
            Status = TenantStatus.Active
        });
        await context.SaveChangesAsync();


        var service = new UserIdentityService(
            context,
            NullLogger<UserIdentityService>.Instance,
            new TestAuditLogWriter(),
            new TestCorrelationContext("corr-1"));

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
            Id = tenantId,
            Name = "Test Tenant",
            Environment = "Testing",
            DefaultCurrency = "USD",
            SupportedCountriesJson = "[]",
            Status = TenantStatus.Active
        });
        await context.SaveChangesAsync();


        var service = new UserIdentityService(
            context,
            NullLogger<UserIdentityService>.Instance,
            new TestAuditLogWriter(),
            new TestCorrelationContext("corr-2"));

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

    [Fact]
    public async Task ResolveOrCreateUserAsync_ShouldLogAuditWithMaskedEmail()
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
            Id = tenantId,
            Name = "Test Tenant",
            Environment = "Testing",
            DefaultCurrency = "USD",
            SupportedCountriesJson = "[]",
            Status = TenantStatus.Active
        });
        await context.SaveChangesAsync();


        var auditLogWriter = new TestAuditLogWriter();
        var correlationId = "corr-verify";
        var service = new UserIdentityService(
            context,
            NullLogger<UserIdentityService>.Instance,
            auditLogWriter,
            new TestCorrelationContext(correlationId));

        // Act
        var user = await service.ResolveOrCreateUserAsync(
            externalIssuer: "test-issuer",
            externalSubject: "test-subject",
            externalTenantId: "external-tenant",
            email: "first@login.test",
            aonikTenantId: tenantId,
            ct: CancellationToken.None);

        // Assert
        auditLogWriter.LastEntry.Should().NotBeNull();
        auditLogWriter.LastEntry!.Action.Should().Be(AuditEventNames.UserProvisioned);
        auditLogWriter.LastEntry.TenantId.Should().Be(tenantId);
        auditLogWriter.LastEntry.ActorId.Should().Be(user.Id);
        auditLogWriter.LastEntry.CorrelationId.Should().Be(correlationId);

        using var document = JsonDocument.Parse(auditLogWriter.LastEntry.DetailsJson!);
        document.RootElement.GetProperty("Email").GetString()
            .Should().Be(AuditLogMasking.MaskEmail("first@login.test"));
    }
}
