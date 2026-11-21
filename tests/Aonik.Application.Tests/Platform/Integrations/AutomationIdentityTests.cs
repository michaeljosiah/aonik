using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

using Aonik.Platform.Entities.Integrations;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Integrations;
using Aonik.Platform.Services.Compliance;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Abstractions.Workflows;
using Aonik.SharedKernel.Abstractions.Observability;
using Aonik.TestSupport.Multitenancy;

namespace Aonik.Application.Tests.Platform.Integrations;

public class AutomationIdentityTests
{
    [Fact]
    public async Task AuditWriter_Should_RecordTheMachineActor_WithoutAPlaceholderHuman()
    {
        using var scenario = new ConnectorScenario();
        await scenario.InitializeAsync(); scenario.Current.Authenticated = false;
        using IServiceScope scope = scenario.Scope();
        PlatformDbContext db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var writer = new AuditLogWriter(db, new TestTenantProvider(scenario.TenantId), scenario.Current, Mock.Of<ICorrelationContext>(), scenario.Clock);
        Guid actorId = Guid.NewGuid(); Guid runId = Guid.NewGuid();

        await writer.LogActorAsync("Connector.ReadCompleted", "ConnectorConnection", Guid.NewGuid(), scenario.TenantId, "AutomationIdentity", actorId, runId.ToString("D"));

        var entry = await db.AuditLogs.SingleAsync();
        entry.ActorType.Should().Be("AutomationIdentity");
        entry.ActorId.Should().Be(actorId);
        entry.CorrelationId.Should().Be(runId.ToString("D"));
    }

    [Fact]
    public async Task Lifecycle_Should_RequireIndependentAdministration_ShowUsage_AndFailClosedAfterDisable()
    {
        Guid versionId = Guid.NewGuid(); Guid invoiceId = Guid.NewGuid();
        using ConnectorScenario scenario = Create(versionId);
        await scenario.InitializeAsync();
        scenario.Allowed.Add((scenario.Current.OwnerId, "Integration.Manage"));
        using IServiceScope scope = scenario.Scope();
        var service = scope.ServiceProvider.GetRequiredService<AutomationIdentityService>();
        AutomationIdentityDetail created = await service.SaveAsync(null, Command(scenario, versionId, invoiceId));
        var actor = new WorkflowActor("AutomationIdentity", created.Id, WorkflowVersionId: versionId);
        var authority = scope.ServiceProvider.GetRequiredService<IAutomationIdentityAuthorization>();
        (await authority.RequireAsync(actor, "Invoice.Issue", "Invoice", invoiceId, new("finance.invoices.issue", 1))).Should().Be(scenario.Current.OwnerId);
        (await service.UsageAsync(created.Id)).ActiveRuns.Should().Be(2);
        AutomationIdentityDetail edited = await service.SaveAsync(created.Id, Command(scenario, versionId, invoiceId) with { Name = "Updated", ExpectedVersion = created.ExpectedVersion });
        edited.Name.Should().Be("Updated");
        (await service.ListAsync()).Should().ContainSingle(identity => identity.Id == created.Id);
        await service.DisableAsync(created.Id, new(edited.ExpectedVersion));
        Func<Task> resume = () => authority.RequireAsync(actor, "Workflow.Run");
        await resume.Should().ThrowAsync<PermissionDeniedException>();
        Func<Task> reactivate = () => service.SaveAsync(created.Id, Command(scenario, versionId, invoiceId) with { ExpectedVersion = edited.ExpectedVersion });
        await reactivate.Should().ThrowAsync<ConnectorAccessException>();
    }

    [Theory]
    [InlineData("manager")]
    [InlineData("delegation")]
    [InlineData("foreign-accountable")]
    [InlineData("unpublished")]
    [InlineData("unknown-action")]
    [InlineData("implicit-scope")]
    [InlineData("mixed-scope")]
    [InlineData("past-expiry")]
    public async Task IdentityChanges_Should_RejectMissingAuthorityAndUnboundedCapabilities(string invalid)
    {
        Guid versionId = Guid.NewGuid();
        using ConnectorScenario scenario = Create(versionId);
        await scenario.InitializeAsync();
        if (invalid != "manager") scenario.Allowed.Add((scenario.Current.OwnerId, "Integration.Manage"));
        if (invalid == "delegation") scenario.Denied.Add((scenario.Current.OwnerId, "Invoice.Issue"));
        AutomationIdentityCommand command = Command(scenario, versionId, Guid.NewGuid());
        if (invalid == "foreign-accountable") command = command with { AccountableUserId = Guid.NewGuid() };
        if (invalid == "unpublished") command = command with { Grants = [command.Grants[0] with { WorkflowVersionId = Guid.NewGuid() }] };
        if (invalid == "unknown-action") command = command with { Grants = [command.Grants[0] with { Actions = [new("finance.capture_payment", 1)] }] };
        if (invalid == "implicit-scope") command = command with { Grants = [command.Grants[0] with { AllSubjects = false, SubjectIds = [] }] };
        if (invalid == "mixed-scope") command = command with { Grants = [command.Grants[0] with { AllSubjects = true }] };
        if (invalid == "past-expiry") command = command with { ExpiresAt = scenario.Clock.UtcNow };
        using IServiceScope scope = scenario.Scope();
        Func<Task> act = () => scope.ServiceProvider.GetRequiredService<AutomationIdentityService>().SaveAsync(null, command);
        await act.Should().ThrowAsync<Exception>();
        (await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().AutomationIdentities.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData("human-claim")]
    [InlineData("foreign-version")]
    [InlineData("foreign-subject")]
    [InlineData("foreign-action")]
    [InlineData("expired")]
    [InlineData("corrupt")]
    public async Task Authority_Should_ValidateActorVersionResourceAndAction_OnEveryRead(string invalid)
    {
        Guid versionId = Guid.NewGuid(); Guid invoiceId = Guid.NewGuid();
        using ConnectorScenario scenario = Create(versionId);
        await scenario.InitializeAsync(); scenario.Allowed.Add((scenario.Current.OwnerId, "Integration.Manage"));
        using IServiceScope scope = scenario.Scope();
        AutomationIdentityDetail identity = await scope.ServiceProvider.GetRequiredService<AutomationIdentityService>().SaveAsync(null, Command(scenario, versionId, invoiceId));
        var actor = new WorkflowActor("AutomationIdentity", identity.Id, WorkflowVersionId: invalid == "foreign-version" ? Guid.NewGuid() : versionId);
        if (invalid == "human-claim") actor = actor with { InitiatingUserId = scenario.Current.OwnerId };
        if (invalid == "expired") scenario.Clock.UtcNow = scenario.Clock.UtcNow.AddDays(2);
        if (invalid == "corrupt")
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            (await db.AutomationIdentities.SingleAsync()).GrantsJson = "not-json"; await db.SaveChangesAsync();
        }
        Func<Task> act = () => scope.ServiceProvider.GetRequiredService<IAutomationIdentityAuthorization>().RequireAsync(actor, "Invoice.Issue", "Invoice",
            invalid == "foreign-subject" ? Guid.NewGuid() : invoiceId, new(invalid == "foreign-action" ? "finance.capture_payment" : "finance.invoices.issue", 1));
        await act.Should().ThrowAsync<PermissionDeniedException>();
    }

    private static AutomationIdentityCommand Command(ConnectorScenario scenario, Guid versionId, Guid invoiceId) => new("Invoice routing", scenario.Current.OwnerId,
        scenario.Clock.UtcNow.AddDays(1), [new(versionId, "Invoice", false, [invoiceId], [new("finance.invoices.issue", 1)])]);

    private static ConnectorScenario Create(Guid publishedVersion)
    {
        var reader = new Mock<IWorkflowAutomationDefinitionReader>();
        reader.Setup(reader => reader.RequireAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid version, Guid _, CancellationToken _) => version == publishedVersion
                ? new AutomationWorkflowCapability(Guid.NewGuid(), version, "hash", [new("finance.invoices.issue", 1, "Invoice.Issue")])
                : throw new ArgumentException("Not published"));
        reader.Setup(reader => reader.GetUsageAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(new AutomationIdentityUsage(2, [publishedVersion]));
        return new(configure: services => { services.AddSingleton(reader.Object); services.AddScoped<AutomationIdentityService>(); });
    }
}
