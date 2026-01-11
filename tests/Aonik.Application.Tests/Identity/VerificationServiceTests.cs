using System.Text.Json;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Aonik.Application.Abstractions.Messaging;
using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Observability;
using Aonik.Application.Services.Compliance;
using Aonik.Application.Services.Identity;
using Aonik.Domain.Identity.Entities;
using Aonik.Infrastructure.Persistence;
using Aonik.Infrastructure.Time;

namespace Aonik.Application.Tests.Identity;

public class VerificationServiceTests
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

    private sealed class TestEmailSender : IEmailSender
    {
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class TestSmsSender : ISmsSender
    {
        public Task SendAsync(SmsMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task StartEmailVerificationAsync_ShouldLogMaskedTarget()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AonikDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        var tenantProvider = new TestTenantProvider(tenantId);
        using var context = new AonikDbContext(options, tenantProvider);
        context.Users.Add(new User
        {
            Id = userId,
            TenantId = tenantId,
            Status = "Active"
        });
        await context.SaveChangesAsync();

        var auditLogWriter = new TestAuditLogWriter();
        var correlationId = "corr-verification";
        var verificationOptions = Options.Create(new VerificationOptions { HashKey = "hash-key" });

        var service = new VerificationService(
            context,
            tenantProvider,
            new TestEmailSender(),
            new TestSmsSender(),
            auditLogWriter,
            new SystemClock(),
            verificationOptions,
            NullLogger<VerificationService>.Instance,
            new TestCorrelationContext(correlationId));

        // Act
        await service.StartEmailVerificationAsync(userId, "jane@example.com", CancellationToken.None);

        // Assert
        auditLogWriter.LastEntry.Should().NotBeNull();
        auditLogWriter.LastEntry!.Action.Should().Be(AuditEventNames.VerificationStarted);
        auditLogWriter.LastEntry.TenantId.Should().Be(tenantId);
        auditLogWriter.LastEntry.ActorId.Should().Be(userId);
        auditLogWriter.LastEntry.CorrelationId.Should().Be(correlationId);

        using var document = JsonDocument.Parse(auditLogWriter.LastEntry.DetailsJson!);
        document.RootElement.GetProperty("Target").GetString()
            .Should().Be(AuditLogMasking.MaskEmail("jane@example.com"));
    }
}
