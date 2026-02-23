using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MicrosoftOptions = Microsoft.Extensions.Options.Options;


using Aonik.Platform.Contracts.Services.Messaging;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Observability;
using Aonik.Platform.Services.Compliance;
using Aonik.Platform.Services.Identity;
using Aonik.Platform.Contracts.Services.Compliance;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.Platform.Entities.Identity;

using Aonik.Platform.Persistence;
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
        public EmailMessage? LastMessage { get; private set; }
        public string? LastCode { get; private set; }

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            LastMessage = message;
            LastCode = ExtractCode(message.Body);
            return Task.CompletedTask;
        }

        private static string? ExtractCode(string body)
        {
            var match = Regex.Match(body, @"\b\d{4,8}\b");
            return match.Success ? match.Value : null;
        }
    }

    private sealed class TestSmsSender : ISmsSender
    {
        public Task SendAsync(SmsMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class AllowAllPermissionService : IPermissionService
    {
        public Task<bool> HasPermissionAsync(Guid userId, string permissionKey, CancellationToken ct = default) =>
            Task.FromResult(true);

        public Task<List<string>> GetUserPermissionsAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(new List<string>());
    }

    [Fact]
    public async Task StartEmailVerificationAsync_ShouldCreateChallenge_AndConfirmSuccessfully()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        var tenantProvider = new TestTenantProvider(tenantId);
        using var context = new PlatformDbContext(options, tenantProvider);
        context.Users.Add(new User
        {
            Id = userId,
            TenantId = tenantId,
            Status = "Active"
        });
        await context.SaveChangesAsync();

        var auditLogWriter = new TestAuditLogWriter();
        var verificationOptions = MicrosoftOptions.Create(new VerificationOptions
        {
            HashKey = "hash-key",
            CodeLength = 6,
            RateLimits = new VerificationRateLimitOptions
            {
                WindowMinutes = 10,
                MaxPerUserChannel = 5,
                MaxPerTargetChannel = 5
            }
        });

        var emailSender = new TestEmailSender();
        var service = new VerificationService(
            context,
            tenantProvider,
            emailSender,
            new TestSmsSender(),
            auditLogWriter,
            new SystemClock(),
            verificationOptions,
            NullLogger<VerificationService>.Instance,
            new TestCorrelationContext("corr-start"),
            new AllowAllPermissionService());

        // Act
        await service.StartEmailVerificationAsync(userId, "jane@example.com", CancellationToken.None);
        var code = emailSender.LastCode;
        code.Should().NotBeNullOrWhiteSpace();

        var confirmed = await service.ConfirmEmailVerificationAsync(userId, "jane@example.com", code!, CancellationToken.None);

        // Assert
        confirmed.Should().BeTrue();
        var challenge = await context.VerificationChallenges.FirstAsync();
        challenge.Status.Should().Be(VerificationStatus.Verified);
        challenge.AttemptCount.Should().Be(0);

        var user = await context.Users.FirstAsync();
        user.Email.Should().Be("jane@example.com");
    }

    [Fact]
    public async Task ConfirmEmailVerificationAsync_ShouldLockChallenge_WhenMaxAttemptsReached()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        var tenantProvider = new TestTenantProvider(tenantId);
        using var context = new PlatformDbContext(options, tenantProvider);
        context.Users.Add(new User
        {
            Id = userId,
            TenantId = tenantId,
            Status = "Active"
        });

        var hashKey = "hash-key";
        var challenge = new VerificationChallenge
        {
            TenantId = tenantId,
            UserId = userId,
            Channel = VerificationChannel.Email,
            Target = "lockout@example.com",
            CodeHash = HashCode("123456", hashKey),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            AttemptCount = 0,
            Status = VerificationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        context.VerificationChallenges.Add(challenge);
        await context.SaveChangesAsync();

        var verificationOptions = MicrosoftOptions.Create(new VerificationOptions
        {
            HashKey = hashKey,
            MaxAttempts = 2
        });

        var service = new VerificationService(
            context,
            tenantProvider,
            new TestEmailSender(),
            new TestSmsSender(),
            new TestAuditLogWriter(),
            new SystemClock(),
            verificationOptions,
            NullLogger<VerificationService>.Instance,
            new TestCorrelationContext(null),
            new AllowAllPermissionService());

        // Act
        var firstAttempt = await service.ConfirmEmailVerificationAsync(userId, "lockout@example.com", "000000", CancellationToken.None);
        var secondAttempt = await service.ConfirmEmailVerificationAsync(userId, "lockout@example.com", "000000", CancellationToken.None);

        // Assert
        firstAttempt.Should().BeFalse();
        secondAttempt.Should().BeFalse();

        var storedChallenge = await context.VerificationChallenges.FirstAsync();
        storedChallenge.AttemptCount.Should().Be(2);
        storedChallenge.Status.Should().Be(VerificationStatus.Failed);
    }

    [Fact]
    public async Task StartPhoneVerificationAsync_ShouldThrow_WhenRateLimitExceeded()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        var tenantProvider = new TestTenantProvider(tenantId);
        using var context = new PlatformDbContext(options, tenantProvider);
        context.Users.Add(new User
        {
            Id = userId,
            TenantId = tenantId,
            Status = "Active"
        });

        var now = DateTime.UtcNow;
        context.VerificationChallenges.Add(new VerificationChallenge
        {
            TenantId = tenantId,
            UserId = userId,
            Channel = VerificationChannel.Sms,
            Target = "+15551234567",
            CodeHash = "hash",
            ExpiresAt = now.AddMinutes(5),
            AttemptCount = 0,
            Status = VerificationStatus.Pending,
            CreatedAt = now
        });
        await context.SaveChangesAsync();

        var verificationOptions = MicrosoftOptions.Create(new VerificationOptions
        {
            HashKey = "hash-key",
            RateLimits = new VerificationRateLimitOptions
            {
                WindowMinutes = 10,
                MaxPerUserChannel = 1,
                MaxPerTargetChannel = 1
            }
        });

        var service = new VerificationService(
            context,
            tenantProvider,
            new TestEmailSender(),
            new TestSmsSender(),
            new TestAuditLogWriter(),
            new SystemClock(),
            verificationOptions,
            NullLogger<VerificationService>.Instance,
            new TestCorrelationContext(null),
            new AllowAllPermissionService());

        // Act
        var act = async () => await service.StartPhoneVerificationAsync(userId, "+15551234567", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Verification rate limit exceeded.");
    }

    [Fact]
    public async Task StartEmailVerificationAsync_ShouldLogMaskedTarget()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        var tenantProvider = new TestTenantProvider(tenantId);
        using var context = new PlatformDbContext(options, tenantProvider);
        context.Users.Add(new User
        {
            Id = userId,
            TenantId = tenantId,
            Status = "Active"
        });
        await context.SaveChangesAsync();

        var auditLogWriter = new TestAuditLogWriter();
        var correlationId = "corr-verification";
        var verificationOptions = MicrosoftOptions.Create(new VerificationOptions { HashKey = "hash-key" });

        var service = new VerificationService(
            context,
            tenantProvider,
            new TestEmailSender(),
            new TestSmsSender(),
            auditLogWriter,
            new SystemClock(),
            verificationOptions,
            NullLogger<VerificationService>.Instance,
            new TestCorrelationContext(correlationId),
            new AllowAllPermissionService());

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

    private static string HashCode(string code, string hashKey)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(hashKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(code));
        return Convert.ToHexString(hash);
    }
}
