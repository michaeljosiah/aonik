using System.Text.Json;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Persistence;
using Aonik.Agents.Services.Workflows;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.SharedKernel.Abstractions.Workflows;
using Aonik.SharedKernel.Events.Outbox;

namespace Aonik.Application.Tests.Agents.Workflows;

public class WorkflowAdmissionTests
{
    [Fact]
    public async Task Start_Should_PinThePublishedRevisionAndServerFacts_AndAtomicallyStageItsWakeup()
    {
        using var host = new Host();
        Guid version = await host.PublishFixture();
        var request = new StartWorkflowRunRequest(version, "Invoice", host.SubjectId, "invoice-approval-1");

        WorkflowRunDetail started = await host.Runs.StartAsync("invoice-route", request);
        WorkflowRunDetail replay = await host.Runs.StartAsync("invoice-route", request);

        replay.Id.Should().Be(started.Id);
        WorkflowRun stored = await host.Db.WorkflowRuns.SingleAsync();
        stored.WorkflowVersionId.Should().Be(version);
        stored.ActorId.Should().Be(host.UserId);
        stored.InitiatingUserId.Should().Be(host.UserId);
        stored.SubjectId.Should().Be(host.SubjectId);
        stored.Status.Should().Be(WorkflowRunStatuses.Queued);
        stored.FactsJson.Should().Contain("1250.25");
        stored.FactsHash.Should().Be(WorkflowFactsHash.Compute(host.Facts.Snapshot));
        stored.CheckpointId.Should().BeNull();
        (await host.Db.Set<OutboxMessage>().CountAsync()).Should().Be(1);
        (await host.Db.Set<OutboxMessage>().SingleAsync()).Payload.Should().Contain(started.Id.ToString());
        (await host.Db.Workflows.SingleAsync()).TriggerCount.Should().Be(1);
    }

    [Fact]
    public async Task Start_Should_Conflict_WhenTheSameKeyIsReusedForAnotherSubject()
    {
        using var host = new Host();
        var request = new StartWorkflowRunRequest(await host.PublishFixture(), "Invoice", host.SubjectId, "stable-key");
        await host.Runs.StartAsync("invoice-route", request);

        Func<Task> changed = async () => { await host.Runs.StartAsync("invoice-route", request with { SubjectId = Guid.NewGuid() }); };

        await changed.Should().ThrowAsync<WorkflowConflictException>();
        (await host.Db.WorkflowRuns.CountAsync()).Should().Be(1);
        (await host.Db.Set<OutboxMessage>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Start_Should_RefuseDisabledExecutionAndUnpublishedOrRetiredGraphs()
    {
        using var host = new Host();
        Guid version = await host.PublishFixture();
        var request = new StartWorkflowRunRequest(version, "Invoice", host.SubjectId, "stable-key");
        host.Enabled = false;
        Func<Task> disabled = async () => { await host.Runs.StartAsync("invoice-route", request); };
        await disabled.Should().ThrowAsync<PermissionDeniedException>();
        host.Enabled = true;
        Workflow workflow = await host.Db.Workflows.SingleAsync();
        workflow.RetiredAt = host.Clock.UtcNow;
        await host.Db.SaveChangesAsync();
        Func<Task> retired = async () => { await host.Runs.StartAsync("invoice-route", request); };
        await retired.Should().ThrowAsync<WorkflowConflictException>();
        workflow.RetiredAt = null;
        workflow.PublishedVersionId = null;
        await host.Db.SaveChangesAsync();
        await retired.Should().ThrowAsync<WorkflowConflictException>();
        (await host.Db.WorkflowRuns.CountAsync()).Should().Be(0);
        (await host.Db.Set<OutboxMessage>().CountAsync()).Should().Be(0);
    }

    private sealed class Host : IDisposable
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid SubjectId { get; } = Guid.NewGuid();
        public bool Enabled { get; set; } = true;
        public AgentsDbContext Db { get; }
        public WorkflowRunService Runs { get; }
        public TestClock Clock { get; } = new();
        public FakeFacts Facts { get; }
        private readonly WorkflowService _workflows;

        public Host()
        {
            var tenant = new Tenant();
            Guid userId = UserId;
            var user = new Mock<ICurrentUserProvider>();
            user.Setup(provider => provider.TryGetCurrentUserId(out userId)).Returns(true);
            user.Setup(provider => provider.GetCurrentUserId()).Returns(userId);
            var permissions = new Mock<IPermissionService>();
            permissions.Setup(service => service.HasPermissionAsync(userId, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            var users = new Mock<IUserDirectoryReader>();
            users.Setup(reader => reader.GetByIdsAsync(tenant.Id, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([new(UserId, null, "Active")]);
            var settings = new Mock<ITenantSettingStore>();
            settings.Setup(store => store.GetTenantValueAsync(WorkflowSettingNames.ExecutionEnabled, tenant.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Enabled ? "true" : "false");
            Db = new(new DbContextOptionsBuilder<AgentsDbContext>().UseInMemoryDatabase($"WorkflowAdmission_{Guid.NewGuid()}").Options,
                tenant, user.Object, Clock);
            var authorization = new WorkflowAuthorization(tenant, user.Object, permissions.Object);
            Facts = new(SubjectId);
            var execution = new WorkflowExecutionAuthorization(tenant, permissions.Object, users.Object, settings.Object,
                new ConfigurationBuilder().Build(), [Facts]);
            _workflows = new(Db, tenant, Clock, authorization);
            Runs = new(Db, tenant, authorization, execution, Clock);
        }

        public async Task<Guid> PublishFixture()
        {
            await _workflows.CreateAsync(new("invoice-route", "Invoice routing", "", "Draft", "v0.1", false, "", null, [],
                [new("start", "trigger", "Start", "", "", 0, 0, "{}"), new("end", "end", "End", "", "", 1, 0, "{}")],
                [new("start", "end", 0, "")], null));
            WorkflowVersion version = await Db.WorkflowVersions.SingleAsync();
            version.PublishedAt = Clock.UtcNow;
            version.PublishedByUserId = UserId;
            Workflow workflow = await Db.Workflows.SingleAsync();
            workflow.PublishedVersionId = version.Id;
            workflow.State = "Active";
            await Db.SaveChangesAsync();
            return version.Id;
        }
        public void Dispose() => Db.Dispose();
    }

    private sealed class Tenant : ITenantProvider
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid GetCurrentTenantId() => Id;
        public bool TryGetCurrentTenantId(out Guid id) { id = Id; return true; }
    }
    private sealed class TestClock : IClock { public DateTime UtcNow => new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc); }
    private sealed class FakeFacts(Guid subjectId) : IWorkflowFactsProvider
    {
        public string SubjectType => "Invoice";
        public string Schema => "invoice-approval.v1";
        public WorkflowFactsSnapshot Snapshot => new(Schema, new Dictionary<string, JsonElement>
        {
            ["invoice.id"] = JsonSerializer.SerializeToElement(subjectId.ToString()),
            ["invoice.total"] = JsonSerializer.SerializeToElement("1250.25"),
            ["invoice.currency"] = JsonSerializer.SerializeToElement("GBP"),
            ["invoice.status"] = JsonSerializer.SerializeToElement("Draft"),
        }, "subject-version-1");
        public Task<WorkflowFactsSnapshot?> LoadAsync(Guid id, WorkflowActor actor, CancellationToken cancellationToken = default)
            => Task.FromResult<WorkflowFactsSnapshot?>(id == subjectId ? Snapshot : null);
    }
}
