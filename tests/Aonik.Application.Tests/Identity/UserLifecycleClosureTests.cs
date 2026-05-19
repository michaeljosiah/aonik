using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Observability;
using Aonik.SharedKernel.Persistence;
using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Authentication;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.Platform.Contracts.Services.Messaging;
using Aonik.Platform.Contracts.Services.Notifications;
using Aonik.Platform.Contracts.Services.Storage;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Identity;

namespace Aonik.Application.Tests.Identity;

/// <summary>
/// Spec 026 acceptance tests for the three P0 closures: invite email
/// + resend, hard delete with tombstone + redaction, and token
/// revocation via the user-session blocklist.
/// </summary>
public class UserLifecycleClosureTests
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

    private sealed class TestCurrentUserProvider : ICurrentUserProvider
    {
        public Guid? UserId { get; set; }
        public Guid? GetCurrentUserId() => UserId;
        public bool TryGetCurrentUserId(out Guid userId)
        {
            userId = UserId ?? Guid.Empty;
            return UserId.HasValue;
        }
    }

    private sealed class AllowAllPermissionService : IPermissionService
    {
        public Task<bool> HasPermissionAsync(Guid userId, string permissionKey, CancellationToken ct = default) =>
            Task.FromResult(true);
        public Task<List<string>> GetUserPermissionsAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(new List<string>());
    }

    private sealed class TestCorrelationContext : ICorrelationContext
    {
        public string? CorrelationId => "corr-test";
    }

    private sealed class CapturingAuditLogWriter : IAuditLogWriter
    {
        public List<(string Action, Guid ResourceId, string? Details)> Events { get; } = new();

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
            Events.Add((action, resourceId, detailsJson));
            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTime now) => UtcNow = now;
        public DateTime UtcNow { get; private set; }
        public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
    }

    private sealed class StubProfilePhotoStore : IProfilePhotoStore
    {
        public Task<PhotoUploadResult> UploadCustomerPhotoAsync(
            Guid tenantId, Guid partyId, string contentType, Stream fileStream, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task DeleteCustomerPhotoAsync(string photoUrl, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public string GetPhotoUrl(string blobPath) => blobPath;
    }

    private sealed class StubNotificationTemplateService : INotificationTemplateService
    {
        public int RenderCount { get; private set; }
        public Task<Aonik.Platform.Contracts.Models.Notifications.RenderNotificationTemplateResult> RenderAsync(
            Aonik.Platform.Contracts.Models.Notifications.RenderNotificationTemplateRequest request,
            CancellationToken cancellationToken = default)
        {
            RenderCount += 1;
            return Task.FromResult(new Aonik.Platform.Contracts.Models.Notifications.RenderNotificationTemplateResult(
                "subject", "<body>hello</body>", Guid.Empty, null));
        }

        public Task<List<Aonik.Platform.Contracts.Models.Notifications.NotificationTemplateSummary>> ListTemplatesAsync(
            string? channel = null, bool? isActive = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<Aonik.Platform.Contracts.Models.Notifications.NotificationTemplateSummary>());
        public Task<Aonik.Platform.Contracts.Models.Notifications.NotificationTemplateResponse?> GetTemplateAsync(
            Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<Aonik.Platform.Contracts.Models.Notifications.NotificationTemplateResponse?>(null);
        public Task<Aonik.Platform.Contracts.Models.Notifications.NotificationTemplateResponse> CreateTemplateAsync(
            Aonik.Platform.Contracts.Models.Notifications.CreateNotificationTemplateRequest request,
            CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Aonik.Platform.Contracts.Models.Notifications.NotificationTemplateResponse> UpdateTemplateAsync(
            Guid id,
            Aonik.Platform.Contracts.Models.Notifications.UpdateNotificationTemplateRequest request,
            CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DeleteTemplateAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Aonik.Platform.Contracts.Models.Notifications.PreviewNotificationTemplateResponse> PreviewTemplateAsync(
            Aonik.Platform.Contracts.Models.Notifications.PreviewNotificationTemplateRequest request,
            CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<Aonik.Platform.Contracts.Models.Notifications.NotificationTemplateBindingResponse>> ListBindingsAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(new List<Aonik.Platform.Contracts.Models.Notifications.NotificationTemplateBindingResponse>());
        public Task<Aonik.Platform.Contracts.Models.Notifications.NotificationTemplateBindingResponse> CreateBindingAsync(
            Aonik.Platform.Contracts.Models.Notifications.CreateNotificationTemplateBindingRequest request,
            CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Aonik.Platform.Contracts.Models.Notifications.NotificationTemplateBindingResponse> UpdateBindingAsync(
            Guid id,
            Aonik.Platform.Contracts.Models.Notifications.UpdateNotificationTemplateBindingRequest request,
            CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DeleteBindingAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class CapturingEmailSender : IEmailSender
    {
        public List<EmailMessage> SentMessages { get; } = new();
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            SentMessages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUserSessionBlocklist : IUserSessionBlocklist
    {
        public List<UserSessionRevocation> Revocations { get; } = new();
        public Task<bool> IsRevokedAsync(Guid tenantId, Guid userId, DateTime tokenIssuedUtc, CancellationToken cancellationToken = default)
        {
            var lastRevoke = Revocations.LastOrDefault(r => r.TenantId == tenantId && r.UserId == userId);
            if (lastRevoke == null) return Task.FromResult(false);
            return Task.FromResult(tokenIssuedUtc < lastRevoke.RevokedUtc);
        }
        public Task<UserSessionRevocation> RevokeAsync(Guid tenantId, Guid userId, Guid? revokedByUserId, string reason, CancellationToken cancellationToken = default)
        {
            var revocation = new UserSessionRevocation(tenantId, userId, DateTime.UtcNow, DateTime.UtcNow.AddDays(14), revokedByUserId, reason);
            Revocations.Add(revocation);
            return Task.FromResult(revocation);
        }
    }

    private sealed class CapturingIdpManagementClient : IIdentityProviderManagementClient
    {
        public List<string> DeletedSubjects { get; } = new();
        public string Provider => "Auth0";
        public bool ShouldSucceed { get; set; } = true;
        public Task<IdpDeleteUserResult> DeleteUserAsync(string externalSubject, string? externalTenantId, CancellationToken cancellationToken = default)
        {
            DeletedSubjects.Add(externalSubject);
            return Task.FromResult(ShouldSucceed
                ? new IdpDeleteUserResult(true, null)
                : new IdpDeleteUserResult(false, "test-failure"));
        }
    }

    private sealed class CapturingIdpManagementClientFactory : IIdentityProviderManagementClientFactory
    {
        public CapturingIdpManagementClient Client { get; } = new();
        public Task<IIdentityProviderManagementClient?> GetClientAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IIdentityProviderManagementClient?>(Client);
    }

    private sealed class TestFixture
    {
        public AccessManagementService Service { get; init; } = null!;
        public PlatformDbContext Context { get; init; } = null!;
        public Guid TenantId { get; init; }
        public Guid OperatorId { get; init; }
        public Role TenantAdminRole { get; init; } = null!;
        public CapturingAuditLogWriter AuditLog { get; init; } = null!;
        public CapturingEmailSender Email { get; init; } = null!;
        public FakeUserSessionBlocklist Blocklist { get; init; } = null!;
        public CapturingIdpManagementClientFactory IdpFactory { get; init; } = null!;
        public FixedClock Clock { get; init; } = null!;
    }

    private static TestFixture CreateFixture()
    {
        var tenantId = Guid.NewGuid();
        var operatorId = Guid.NewGuid();
        var clock = new FixedClock(new DateTime(2026, 5, 19, 12, 0, 0, DateTimeKind.Utc));
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(databaseName: $"LifecycleTests_{Guid.NewGuid()}")
            .Options;
        var dbContext = new PlatformDbContext(options, new TestTenantProvider(tenantId));

        var tenantAdminRole = new Role
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "TenantAdmin",
        };
        dbContext.Roles.Add(tenantAdminRole);
        // The operator is an existing TenantAdmin so the "last-admin"
        // guard in DeleteUserAsync doesn't trip on the test user being
        // deleted (we ensure another admin exists).
        var operatorUser = new User
        {
            Id = operatorId,
            TenantId = tenantId,
            ExternalIssuer = "test-iss",
            ExternalSubject = "operator-sub",
            Email = "operator@example.com",
            Status = "Active",
        };
        dbContext.Users.Add(operatorUser);
        dbContext.UserRoles.Add(new UserRole
        {
            Id = Guid.NewGuid(),
            UserId = operatorId,
            RoleId = tenantAdminRole.Id,
            CreatedAt = clock.UtcNow,
        });
        dbContext.SaveChanges();

        var auditLog = new CapturingAuditLogWriter();
        var correlation = new TestCorrelationContext();
        var currentUserProvider = new TestCurrentUserProvider { UserId = operatorId };
        var provisioner = new Aonik.Platform.Services.Identity.PendingTenantUserProvisioner(
            dbContext, clock, currentUserProvider, auditLog, correlation);

        var templateService = new StubNotificationTemplateService();
        var emailSender = new CapturingEmailSender();
        var blocklist = new FakeUserSessionBlocklist();
        var idpFactory = new CapturingIdpManagementClientFactory();

        var service = new AccessManagementService(
            dbContext,
            new TestTenantProvider(tenantId),
            currentUserProvider,
            new AllowAllPermissionService(),
            clock,
            auditLog,
            correlation,
            new StubProfilePhotoStore(),
            provisioner,
            templateService,
            emailSender,
            Microsoft.Extensions.Options.Options.Create(new UserLifecycleOptions
            {
                MaxInviteSendsPer24Hours = 3,
                InviteTokenTtlHours = 72,
            }),
            blocklist,
            idpFactory,
            NullLoggerFactory.Instance);

        return new TestFixture
        {
            Service = service,
            Context = dbContext,
            TenantId = tenantId,
            OperatorId = operatorId,
            TenantAdminRole = tenantAdminRole,
            AuditLog = auditLog,
            Email = emailSender,
            Blocklist = blocklist,
            IdpFactory = idpFactory,
            Clock = clock,
        };
    }

    // ── Part 1 — invite email ────────────────────────────────────

    [Fact]
    public async Task InviteUser_Should_SendEmail_And_RotateTokenOnResend()
    {
        var f = CreateFixture();

        var invite = await f.Service.InviteUserAsync(new InviteUserRequest(
            Email: "invitee@example.com",
            RoleIds: new List<Guid> { f.TenantAdminRole.Id }),
            CancellationToken.None);

        invite.EmailSent.Should().BeTrue();
        invite.EmailSendCount.Should().Be(1);
        invite.ExpiresUtc.Should().NotBeNull();
        f.Email.SentMessages.Should().ContainSingle();

        var stored = await f.Context.Users.FirstAsync(u => u.Id == invite.UserId);
        stored.InviteToken.Should().NotBeNullOrEmpty();
        var initialToken = stored.InviteToken;
        stored.InviteEmailSendCount.Should().Be(1);

        // Resend rotates the token and increments the counter.
        var resend = await f.Service.ResendInviteAsync(invite.UserId, CancellationToken.None);
        resend.EmailSent.Should().BeTrue();
        resend.EmailSendCount.Should().Be(2);

        var afterResend = await f.Context.Users.FirstAsync(u => u.Id == invite.UserId);
        afterResend.InviteToken.Should().NotBeNullOrEmpty();
        afterResend.InviteToken.Should().NotBe(initialToken);

        f.Email.SentMessages.Should().HaveCount(2);
        f.AuditLog.Events.Select(e => e.Action).Should().Contain(AuditEventNames.UserInviteEmailSent);
    }

    [Fact]
    public async Task ResendInvite_Should_RateLimit_After_MaxSendsIn24Hours()
    {
        var f = CreateFixture();

        var invite = await f.Service.InviteUserAsync(new InviteUserRequest(
            Email: "invitee@example.com"),
            CancellationToken.None);

        // Two more sends — total 3 (matches the configured cap).
        await f.Service.ResendInviteAsync(invite.UserId, CancellationToken.None);
        await f.Service.ResendInviteAsync(invite.UserId, CancellationToken.None);

        // 4th send should be blocked.
        var blocked = await f.Service.ResendInviteAsync(invite.UserId, CancellationToken.None);
        blocked.EmailSent.Should().BeFalse();
        blocked.RateLimitReason.Should().NotBeNullOrEmpty();
    }

    // ── Part 3 — token revocation ──────────────────────────────

    [Fact]
    public async Task RevokeSessions_Should_AppendBlocklistRow_And_AuditEvent()
    {
        var f = CreateFixture();
        var invite = await f.Service.InviteUserAsync(new InviteUserRequest(Email: "target@example.com"), CancellationToken.None);

        // Promote the placeholder to a real user so revoke applies to a
        // non-placeholder identity (the operator UI would only enable
        // revoke for active users).
        var user = await f.Context.Users.FirstAsync(u => u.Id == invite.UserId);
        user.ExternalIssuer = "real-iss";
        user.ExternalSubject = "real-sub";
        await f.Context.SaveChangesAsync();

        var result = await f.Service.RevokeSessionsAsync(
            invite.UserId,
            new RevokeUserSessionsRequest("laptop stolen"),
            CancellationToken.None);

        result.UserId.Should().Be(invite.UserId);
        result.Reason.Should().Be("laptop stolen");
        f.Blocklist.Revocations.Should().ContainSingle().Which.Reason.Should().Be("laptop stolen");
        f.AuditLog.Events.Select(e => e.Action).Should().Contain(AuditEventNames.UserSessionsRevoked);
    }

    [Fact]
    public async Task DeactivateUser_Should_AlsoRevokeSessions_AutoRevoke()
    {
        var f = CreateFixture();
        var invite = await f.Service.InviteUserAsync(new InviteUserRequest(Email: "deact@example.com"), CancellationToken.None);
        var user = await f.Context.Users.FirstAsync(u => u.Id == invite.UserId);
        user.ExternalIssuer = "real-iss";
        user.ExternalSubject = "real-sub";
        await f.Context.SaveChangesAsync();

        await f.Service.DeactivateUserAsync(invite.UserId, CancellationToken.None);

        f.Blocklist.Revocations.Should().ContainSingle();
        f.Blocklist.Revocations[0].Reason.Should().Be("auto-revoke on deactivate");
        var afterDeact = await f.Context.Users.FirstAsync(u => u.Id == invite.UserId);
        afterDeact.Status.Should().Be("Deactivated");
    }

    // ── Part 2 — hard delete with tombstone + redaction ─────────

    [Fact]
    public async Task DeleteUser_Should_CreateTombstone_And_RemoveUserRow_And_DeleteIdp()
    {
        var f = CreateFixture();
        var invite = await f.Service.InviteUserAsync(new InviteUserRequest(Email: "target@example.com"), CancellationToken.None);
        var user = await f.Context.Users.FirstAsync(u => u.Id == invite.UserId);
        user.ExternalIssuer = "real-iss";
        user.ExternalSubject = "real-sub-xyz";
        user.Status = "Active";
        await f.Context.SaveChangesAsync();

        // Seed a couple of audit rows that mention the user's email,
        // so we can assert the redaction step ran.
        f.Context.AuditLogs.Add(new Aonik.Platform.Entities.Compliance.AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = f.TenantId,
            Timestamp = f.Clock.UtcNow,
            ActorType = "User",
            ActorId = user.Id,
            Action = "TestEvent",
            ResourceType = "User",
            ResourceId = user.Id,
            DetailsJson = "{\"Email\":\"target@example.com\"}",
            CorrelationId = "x",
            CreatedAt = f.Clock.UtcNow,
        });
        await f.Context.SaveChangesAsync();

        var result = await f.Service.DeleteUserAsync(
            invite.UserId,
            new DeleteUserRequest("target@example.com", "GDPR erasure request received"),
            CancellationToken.None);

        result.IdentityProviderUserDeleted.Should().BeTrue();
        result.AuditRowsRedacted.Should().BeGreaterThanOrEqualTo(1);

        // User row is gone…
        (await f.Context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == invite.UserId)).Should().BeNull();
        // …tombstone is retained.
        var tombstone = await f.Context.UserTombstones.AsNoTracking()
            .AcrossTenants()
            .FirstOrDefaultAsync(t => t.OriginalUserId == invite.UserId);
        tombstone.Should().NotBeNull();
        tombstone!.Reason.Should().Be("GDPR erasure request received");
        tombstone.MaskedEmail.Should().NotContain("target@example.com");

        // IdP delete was called with the correct external subject.
        f.IdpFactory.Client.DeletedSubjects.Should().ContainSingle().Which.Should().Be("real-sub-xyz");

        // Audit-log row's email is redacted.
        var redactedRow = await f.Context.AuditLogs.AsNoTracking()
            .AcrossTenants()
            .FirstAsync(a => a.ResourceId == tombstone.OriginalUserId || a.ActorId == tombstone.OriginalUserId);
        redactedRow.DetailsJson.Should().NotContain("target@example.com");
    }

    [Fact]
    public async Task DeleteUser_Should_Reject_When_EmailConfirmationDoesNotMatch()
    {
        var f = CreateFixture();
        var invite = await f.Service.InviteUserAsync(new InviteUserRequest(Email: "target@example.com"), CancellationToken.None);

        Func<Task> act = () => f.Service.DeleteUserAsync(
            invite.UserId,
            new DeleteUserRequest("wrong@example.com", "GDPR erasure request received"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*confirmation*");
    }

    [Fact]
    public async Task DeleteUser_Should_Reject_When_ReasonTooShort()
    {
        var f = CreateFixture();
        var invite = await f.Service.InviteUserAsync(new InviteUserRequest(Email: "target@example.com"), CancellationToken.None);

        Func<Task> act = () => f.Service.DeleteUserAsync(
            invite.UserId,
            new DeleteUserRequest("target@example.com", "too short"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ListTombstones_Should_ReturnPagedSummaries_OrderedByDeletedDesc()
    {
        var f = CreateFixture();
        var a = await f.Service.InviteUserAsync(new InviteUserRequest(Email: "a@example.com"), CancellationToken.None);
        var b = await f.Service.InviteUserAsync(new InviteUserRequest(Email: "b@example.com"), CancellationToken.None);

        foreach (var id in new[] { a.UserId, b.UserId })
        {
            var u = await f.Context.Users.FirstAsync(x => x.Id == id);
            u.ExternalIssuer = "real-iss";
            u.ExternalSubject = $"real-sub-{id}";
            u.Status = "Active";
        }
        await f.Context.SaveChangesAsync();

        await f.Service.DeleteUserAsync(a.UserId, new DeleteUserRequest("a@example.com", "reason for a"), CancellationToken.None);
        f.Clock.Advance(TimeSpan.FromMinutes(1));
        await f.Service.DeleteUserAsync(b.UserId, new DeleteUserRequest("b@example.com", "reason for b"), CancellationToken.None);

        var page = await f.Service.ListTombstonesAsync(new ListUsersRequest(), CancellationToken.None);
        page.Items.Should().HaveCount(2);
        var firstId = page.Items[0].OriginalUserId;
        (firstId == a.UserId || firstId == b.UserId).Should().BeTrue();
        page.TotalCount.Should().Be(2);
    }
}
