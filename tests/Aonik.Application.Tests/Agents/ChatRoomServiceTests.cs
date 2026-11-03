using System.Text.Json;
using Aonik.SharedKernel.Events.Integration;
using Aonik.SharedKernel.Events.Outbox;
using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Entities;
using Aonik.Agents.Persistence;
using Aonik.Agents.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Aonik.Application.Tests.Agents;

public sealed class ChatRoomServiceTests
{
    private sealed class Fixture : IDisposable
    {
        public readonly Guid Tenant = Guid.NewGuid(), Owner = Guid.NewGuid(), Colleague = Guid.NewGuid(), Observer = Guid.NewGuid();
        public readonly DateTime Now = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);
        public readonly Mock<IChatParticipantDirectory> Directory = new();
        public readonly Mock<IChatRoomAgentRunner> Runner = new();
        public readonly Mock<IAiRunWriter> Runs = new();
        public readonly Mock<IToolApprovalService> Approvals = new();
        public readonly AgentsDbContext Db;
        public readonly ServiceProvider Provider;
        public readonly HashSet<Guid> Staff = [];
        public readonly HashSet<Guid> ReadOnly = [];
        private readonly string _database = "ChatRooms_" + Guid.NewGuid();
        public Fixture()
        {
            Staff.UnionWith([Owner, Colleague, Observer]);
            Directory.Setup(directory => directory.GetByIdsAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid tenant, IReadOnlyCollection<Guid> ids, CancellationToken _) => (IReadOnlyList<ChatWorkspacePerson>)(tenant == Tenant ? ids.Where(Staff.Contains).Select(id => new ChatWorkspacePerson(id, id == Owner ? "Oliver Reed" : id == Colleague ? "Maria Gomez" : "Sam Chen", !ReadOnly.Contains(id))).ToList() : []));
            Directory.Setup(directory => directory.SearchAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
            Runner.Setup(runner => runner.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new("master-orchestrator", "AONIK", "Room context"), new("finance-agent", "Finance", "Domain tools"), new("platform-agent", "Platform", "Domain tools")]);
            Runner.Setup(runner => runner.RunAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new RoomAgentResult("The shared records are ready for review.", []));
            Runs.Setup(runs => runs.StartRunAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => Guid.NewGuid());
            Provider = new ServiceCollection().AddScoped(_ => CreateDb()).AddSingleton(Directory.Object).BuildServiceProvider();
            Db = CreateDb();
        }
        public AgentsDbContext CreateDb(IInterceptor? interceptor = null)
        {
            var options = new DbContextOptionsBuilder<AgentsDbContext>();
            var sql = Environment.GetEnvironmentVariable("AONIK_CHATROOM_TEST_SQL_CONNECTION");
            if (string.IsNullOrWhiteSpace(sql)) options.UseInMemoryDatabase(_database);
            else options.UseSqlServer(sql);
            if (interceptor is not null) options.AddInterceptors(interceptor);
            return new AgentsDbContext(options.Options, new TenantProvider(Tenant), new UserProvider(Owner), Mock.Of<IClock>(clock => clock.UtcNow == Now));
        }
        public ChatRoomService Service(Guid user, AgentsDbContext? db = null, Guid? tenant = null) => new(db ?? Db, new TenantProvider(tenant ?? Tenant), new UserProvider(user), Directory.Object, Runner.Object, Runs.Object, Approvals.Object, new ChatRoomInvocationGuard(Provider.GetRequiredService<IServiceScopeFactory>()), Mock.Of<IClock>(clock => clock.UtcNow == Now), NullLogger<ChatRoomService>.Instance);
        public Task<ChatRoomDetail> Create() => Service(Owner).CreateAsync(new("Collections review", [new("Invoice", "INV-2048")]));
        public void Dispose() { Db.Dispose(); Provider.Dispose(); }
    }
    private sealed class InterleavedPostInterceptor(Func<Task> post) : SaveChangesInterceptor
    {
        private bool _posted;
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!_posted && eventData.Context!.ChangeTracker.Entries<ChatRoomMessage>().Any(entry => entry.State == EntityState.Added && entry.Entity.ActorKind == "Agent"))
            {
                _posted = true;
                await post();
            }
            return result;
        }
    }
    private sealed class InterruptedReplyInterceptor : SaveChangesInterceptor
    {
        private bool _interrupted;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!_interrupted && eventData.Context!.ChangeTracker.Entries<ChatRoomMessage>().Any(entry => entry.State == EntityState.Added && entry.Entity.ActorKind == "Agent"))
            {
                _interrupted = true;
                throw new InvalidOperationException("Simulated interruption before the reply commit.");
            }
            return ValueTask.FromResult(result);
        }
    }
    private sealed class TenantProvider(Guid tenant) : ITenantProvider { public Guid GetCurrentTenantId() => tenant; public bool TryGetCurrentTenantId(out Guid id) { id = tenant; return true; } }
    private sealed class UserProvider(Guid user) : ICurrentUserProvider { public Guid? GetCurrentUserId() => user == Guid.Empty ? null : user; public bool TryGetCurrentUserId(out Guid id) { id = user; return id != Guid.Empty; } }

    [Fact]
    public async Task Create_ShouldKeepPrivateHistorySeparate_AndRegisterOwnerAndAonik()
    {
        using var f = new Fixture();
        f.Db.ChatThreads.Add(new ChatThread { TenantId = f.Tenant, UserId = f.Owner, Title = "Private", Messages = [new() { TenantId = f.Tenant, Role = "user", Content = "Private bank details" }] });
        await f.Db.SaveChangesAsync();
        var room = await f.Create();
        room.Participants.Should().HaveCount(2);
        room.Participants.Should().Contain(member => member.UserId == f.Owner && member.Role == "Owner");
        room.Participants.Should().Contain(member => member.AgentName == "master-orchestrator");
        room.Messages.Should().OnlyContain(message => !message.Content.Contains("Private bank details"));
        room.CurrentUserId.Should().Be(f.Owner);
    }

    [Fact]
    public async Task Access_ShouldFailClosed_ForNonMemberCrossTenantAndMissingUser()
    {
        using var f = new Fixture(); var room = await f.Create();
        await f.Service(f.Colleague).Invoking(service => service.GetAsync(room.Room.Id)).Should().ThrowAsync<ChatRoomException>().Where(error => error.StatusCode == 404);
        (await f.Service(f.Colleague).ListAsync()).Should().BeEmpty();
        await f.Service(f.Owner, tenant: Guid.NewGuid()).Invoking(service => service.GetAsync(room.Room.Id)).Should().ThrowAsync<ChatRoomException>();
        await f.Service(Guid.Empty).Invoking(service => service.ListAsync()).Should().ThrowAsync<ChatRoomException>().Where(error => error.StatusCode == 401);
    }

    [Fact]
    public async Task Invitation_ShouldRequireOwnerActiveStaffAndHistoryAcknowledgement()
    {
        using var f = new Fixture(); var room = await f.Create(); var owner = f.Service(f.Owner);
        await owner.Invoking(service => service.AddMemberAsync(room.Room.Id, new($"human:{f.Colleague}", "Participant", false))).Should().ThrowAsync<ChatRoomException>();
        await owner.Invoking(service => service.AddMemberAsync(room.Room.Id, new($"human:{Guid.NewGuid()}", "Participant", true))).Should().ThrowAsync<ChatRoomException>();
        await owner.AddMemberAsync(room.Room.Id, new($"human:{f.Colleague}", "Participant", true));
        var colleague = f.Service(f.Colleague);
        (await colleague.GetAsync(room.Room.Id)).Messages.Should().NotBeEmpty();
        await colleague.Invoking(service => service.AddMemberAsync(room.Room.Id, new($"human:{f.Observer}", "Participant", true))).Should().ThrowAsync<ChatRoomException>().Where(error => error.StatusCode == 403);
    }

    [Fact]
    public async Task Observer_ShouldReadButNeverPostInvokeOrDecide()
    {
        using var f = new Fixture(); var room = await f.Create();
        await f.Service(f.Owner).AddMemberAsync(room.Room.Id, new($"human:{f.Observer}", "Observer", true));
        var observer = f.Service(f.Observer);
        (await observer.GetAsync(room.Room.Id)).Room.Role.Should().Be("Observer");
        await observer.Invoking(service => service.SendAsync(room.Room.Id, new(Guid.NewGuid(), "Hello"))).Should().ThrowAsync<ChatRoomException>().Where(error => error.StatusCode == 403);
        await observer.Invoking(service => service.RunAgentAsync(room.Room.Id, new(Guid.NewGuid(), Guid.NewGuid(), "master-orchestrator"))).Should().ThrowAsync<ChatRoomException>().Where(error => error.StatusCode == 403);
        await observer.Invoking(service => service.DecideAsync(room.Room.Id, new(Guid.NewGuid(), Guid.NewGuid(), "Approve"))).Should().ThrowAsync<ChatRoomException>().Where(error => error.StatusCode == 403);
    }

    [Fact]
    public async Task Revocation_ShouldImmediatelyHideHistoryAndRejectFurtherWrites()
    {
        using var f = new Fixture(); var room = await f.Create(); var owner = f.Service(f.Owner);
        await owner.AddMemberAsync(room.Room.Id, new($"human:{f.Colleague}", "Participant", true));
        await owner.RemoveMemberAsync(room.Room.Id, $"human:{f.Colleague}");
        (await f.Service(f.Colleague).ListAsync()).Should().BeEmpty();
        await f.Service(f.Colleague).Invoking(service => service.GetAsync(room.Room.Id)).Should().ThrowAsync<ChatRoomException>();
        await owner.Invoking(service => service.RemoveMemberAsync(room.Room.Id, $"human:{f.Owner}")).Should().ThrowAsync<ChatRoomException>();
    }

    [Fact]
    public async Task Send_ShouldAssignServerAuthorValidateMentionsAndDeduplicateRetries()
    {
        using var f = new Fixture(); var room = await f.Create(); var owner = f.Service(f.Owner);
        var request = new SendRoomMessageRequest(Guid.NewGuid(), "Review this invoice", ["agent:master-orchestrator"]);
        var first = await owner.SendAsync(room.Room.Id, request); var retry = await owner.SendAsync(room.Room.Id, request);
        retry.Id.Should().Be(first.Id); first.AuthorUserId.Should().Be(f.Owner); first.AuthorName.Should().Be("Oliver Reed");
        await owner.Invoking(service => service.SendAsync(room.Room.Id, request with { Content = "Changed" })).Should().ThrowAsync<ChatRoomException>().Where(error => error.StatusCode == 409);
        await owner.Invoking(service => service.SendAsync(room.Room.Id, new(Guid.NewGuid(), "Secret mention", [$"human:{Guid.NewGuid()}"]))).Should().ThrowAsync<ChatRoomException>();
        (await owner.GetAsync(room.Room.Id)).Messages.Count(message => message.ActorKind == "Human").Should().Be(1);
    }

    [Fact]
    public async Task AgentRun_ShouldUseOnlySharedHistoryAndPersistDistinctAuthorAndAiRun()
    {
        using var f = new Fixture(); var room = await f.Create(); var owner = f.Service(f.Owner);
        await owner.AddMemberAsync(room.Room.Id, new("agent:finance-agent", "Agent", true));
        var message = await owner.SendAsync(room.Room.Id, new(Guid.NewGuid(), "Review INV-2048"));
        var request = new RunRoomAgentRequest(Guid.NewGuid(), message.Id, "finance-agent");
        var turn = await owner.RunAgentAsync(room.Room.Id, request);
        turn.Status.Should().Be("Completed"); turn.AiRunId.Should().NotBeNull();
        var reply = (await owner.GetAsync(room.Room.Id)).Messages.Single(message => message.ActorKind == "Agent");
        reply.AgentName.Should().Be("finance-agent"); reply.AiRunId.Should().Be(turn.AiRunId);
        (await owner.RunAgentAsync(room.Room.Id, request)).Id.Should().Be(turn.Id);
        f.Runner.Verify(runner => runner.RunAsync("finance-agent", It.Is<string>(context => context.Contains("INV-2048") && context.Contains("All current room participants")), It.IsAny<CancellationToken>()), Times.Once);
        f.Runs.Verify(runs => runs.StartRunAsync("collaborative-chat", It.Is<string>(json => json.Contains("promptVersion") && !json.Contains("Review INV-2048")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AgentRun_ShouldRejectUnknownAgentsOtherHumansPromptsAndConcurrentTurns()
    {
        using var f = new Fixture(); var room = await f.Create(); var owner = f.Service(f.Owner);
        var prompt = await owner.SendAsync(room.Room.Id, new(Guid.NewGuid(), "Review"));
        await owner.Invoking(service => service.AddMemberAsync(room.Room.Id, new("agent:billing-agent", "Agent", true))).Should().ThrowAsync<ChatRoomException>();
        await owner.AddMemberAsync(room.Room.Id, new($"human:{f.Colleague}", "Participant", true));
        await f.Service(f.Colleague).Invoking(service => service.RunAgentAsync(room.Room.Id, new(Guid.NewGuid(), prompt.Id, "master-orchestrator"))).Should().ThrowAsync<ChatRoomException>();
        var entity = await f.Db.ChatRooms.SingleAsync(); entity.ActiveTurnId = Guid.NewGuid(); await f.Db.SaveChangesAsync();
        await owner.Invoking(service => service.RunAgentAsync(room.Room.Id, new(Guid.NewGuid(), prompt.Id, "master-orchestrator"))).Should().ThrowAsync<ChatRoomException>().Where(error => error.StatusCode == 409);
        f.Runner.Verify(runner => runner.RunAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InterruptedRun_ShouldBeUnconfirmedAndNeverAutomaticallyReexecute()
    {
        using var f = new Fixture(); var room = await f.Create(); var owner = f.Service(f.Owner);
        f.Runner.Setup(runner => runner.RunAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new RoomAgentResult("", [], "Interrupted"));
        var prompt = await owner.SendAsync(room.Room.Id, new(Guid.NewGuid(), "Review"));
        var request = new RunRoomAgentRequest(Guid.NewGuid(), prompt.Id, "master-orchestrator");
        (await owner.RunAgentAsync(room.Room.Id, request)).Status.Should().Be("Unconfirmed");
        (await owner.RunAgentAsync(room.Room.Id, request)).Status.Should().Be("Unconfirmed");
        f.Runner.Verify(runner => runner.RunAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        (await f.Db.ChatRooms.SingleAsync()).ActiveTurnId.Should().BeNull();
    }

    [Fact]
    public async Task PresenceAndPagination_ShouldReflectPersistedParticipantsAndSequence()
    {
        using var f = new Fixture(); var room = await f.Create(); var owner = f.Service(f.Owner);
        await owner.HeartbeatAsync(room.Room.Id);
        for (var i = 0; i < 104; i++) await owner.SendAsync(room.Room.Id, new(Guid.NewGuid(), $"Message {i}"));
        var latest = await owner.GetAsync(room.Room.Id);
        latest.Messages.Should().HaveCount(100); latest.HasEarlier.Should().BeTrue();
        latest.Participants.Single(person => person.UserId == f.Owner).LastSeenAt.Should().Be(f.Now);
        var earlier = await owner.GetAsync(room.Room.Id, latest.Messages[0].Sequence);
        earlier.Messages.Should().HaveCount(5); earlier.HasEarlier.Should().BeFalse();
        latest.Messages.Concat(earlier.Messages).Select(message => message.Sequence).Distinct().Should().HaveCount(105);
    }

    [Fact]
    public async Task ReadOnlyWorkspaceRole_ShouldNeverGainAgentAuthorityFromRoomMembership()
    {
        using var f = new Fixture(); var room = await f.Create(); var owner = f.Service(f.Owner);
        await owner.AddMemberAsync(room.Room.Id, new($"human:{f.Colleague}", "Participant", true));
        f.ReadOnly.Add(f.Colleague);
        var colleague = f.Service(f.Colleague);
        var prompt = await colleague.SendAsync(room.Room.Id, new(Guid.NewGuid(), "Discuss the invoice"));
        await colleague.Invoking(service => service.RunAgentAsync(room.Room.Id, new(Guid.NewGuid(), prompt.Id, "master-orchestrator"))).Should().ThrowAsync<ChatRoomException>().Where(error => error.StatusCode == 403);
        f.Runner.Verify(runner => runner.RunAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConcurrentWriters_ShouldRejectStaleRoomRevisions()
    {
        using var f = new Fixture(); var room = await f.Create();
        using var firstDb = f.CreateDb(); using var secondDb = f.CreateDb();
        await firstDb.ChatRooms.SingleAsync(); await secondDb.ChatRooms.SingleAsync();
        await f.Service(f.Owner, firstDb).SendAsync(room.Room.Id, new(Guid.NewGuid(), "First"));
        var secondRequest = new SendRoomMessageRequest(Guid.NewGuid(), "Second");
        await f.Service(f.Owner, secondDb).Invoking(service => service.SendAsync(room.Room.Id, secondRequest)).Should().ThrowAsync<ChatRoomException>().Where(error => error.StatusCode == 409);
        // Atomic rollback is a SQL Server guarantee. EF InMemory does not implement transactions.
        if (!secondDb.Database.IsRelational()) return;
        using var retryDb = f.CreateDb();
        await f.Service(f.Owner, retryDb).SendAsync(room.Room.Id, secondRequest);
        var sequences = await retryDb.ChatRoomMessages.OrderBy(message => message.Sequence).Select(message => message.Sequence).ToListAsync();
        sequences.Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task ApprovalCards_ShouldRemainRequesterOwnedAndBoundToTheRoomTurn()
    {
        using var f = new Fixture(); var room = await f.Create(); var owner = f.Service(f.Owner);
        await owner.AddMemberAsync(room.Room.Id, new($"human:{f.Colleague}", "Participant", true));
        var approval = new ToolApprovalRequest { TenantId = f.Tenant, RequestingUserId = f.Owner, ToolName = "finance_create_invoice", RiskTier = "Medium", ActionKind = "Create draft", ArgumentsRedactedJson = "{}", ExpiresAt = f.Now.AddMinutes(10) };
        f.Db.ToolApprovalRequests.Add(approval); await f.Db.SaveChangesAsync();
        f.Runner.Setup(runner => runner.RunAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new RoomAgentResult("Review the proposed draft", [approval.Id]));
        var prompt = await owner.SendAsync(room.Room.Id, new(Guid.NewGuid(), "Prepare a draft"));
        var turn = await owner.RunAgentAsync(room.Room.Id, new(Guid.NewGuid(), prompt.Id, "master-orchestrator"));
        turn.Approvals.Single().CanDecide.Should().BeTrue();
        var colleague = f.Service(f.Colleague);
        (await colleague.GetAsync(room.Room.Id)).Turns.Single().Approvals.Single().CanDecide.Should().BeFalse();
        await colleague.Invoking(service => service.DecideAsync(room.Room.Id, new(turn.Id, approval.Id, "Approve"))).Should().ThrowAsync<ChatRoomException>();
        await owner.Invoking(service => service.DecideAsync(room.Room.Id, new(turn.Id, Guid.NewGuid(), "Approve"))).Should().ThrowAsync<ChatRoomException>();
        f.Approvals.Verify(service => service.DecideAsync(It.IsAny<Guid>(), It.IsAny<ToolApprovalDecisionInput>(), It.IsAny<CancellationToken>()), Times.Never);
        approval.Status = ToolApprovalRequestStatus.Approved; await f.Db.SaveChangesAsync();
        (await owner.GetAsync(room.Room.Id)).Turns.Single().Approvals.Single().CanContinue.Should().BeTrue();
        approval.ConsumedAt = f.Now; await f.Db.SaveChangesAsync();
        (await owner.GetAsync(room.Room.Id)).Turns.Single().Approvals.Single().CanContinue.Should().BeFalse();
    }

    [Fact]
    public async Task GlobalProposalRoutes_ShouldRespectRoomOriginAndMembershipRevocation()
    {
        using var f = new Fixture(); var room = await f.Create(); var owner = f.Service(f.Owner);
        await owner.AddMemberAsync(room.Room.Id, new($"human:{f.Colleague}", "Participant", true));
        var colleague = f.Service(f.Colleague);
        var prompt = await colleague.SendAsync(room.Room.Id, new(Guid.NewGuid(), "Review the references"));
        var turn = await colleague.RunAgentAsync(room.Room.Id, new(Guid.NewGuid(), prompt.Id, "master-orchestrator"));
        var authority = new ChatRoomApprovalAuthority(f.Db, new UserProvider(f.Colleague), f.Directory.Object);
        (await authority.CanDecideAsync(turn.AiRunId)).Should().BeTrue();
        (await new ChatRoomApprovalAuthority(f.Db, new UserProvider(f.Owner), f.Directory.Object).CanDecideAsync(turn.AiRunId)).Should().BeFalse();
        await owner.RemoveMemberAsync(room.Room.Id, $"human:{f.Colleague}");
        (await authority.CanDecideAsync(turn.AiRunId)).Should().BeFalse();
        var proposal = new Proposal { TenantId = f.Tenant, AiRunId = turn.AiRunId!.Value, ProposalType = "Finance.CapturePayment", RiskTier = "High", Status = ProposalStatus.Proposed };
        f.Db.Proposals.Add(proposal); await f.Db.SaveChangesAsync();
        var dispatcher = new Mock<IProposalDispatcher>(); var rejection = new Mock<IProposalRejectionDispatcher>();
        var service = new ProposalApprovalService(f.Db, new UserProvider(f.Colleague), Mock.Of<IClock>(clock => clock.UtcNow == f.Now), dispatcher.Object, rejection.Object, roomApprovalAuthority: authority);
        await service.Invoking(value => value.ApproveAsync(proposal.Id)).Should().ThrowAsync<PermissionDeniedException>();
        await service.Invoking(value => value.DismissAsync(proposal.Id)).Should().ThrowAsync<PermissionDeniedException>();
        dispatcher.Invocations.Should().BeEmpty(); rejection.Invocations.Should().BeEmpty();
        proposal.Status.Should().Be(ProposalStatus.Proposed);
    }

    [Fact]
    public async Task AgentReply_ShouldKeepSequenceAndNeverRerunTools_WhenAnotherHumanPosts()
    {
        using var f = new Fixture(); var room = await f.Create();
        await f.Service(f.Owner).AddMemberAsync(room.Room.Id, new($"human:{f.Colleague}", "Participant", true));
        using var otherDb = f.CreateDb();
        async Task Post() => await f.Service(f.Colleague, otherDb).SendAsync(room.Room.Id, new(Guid.NewGuid(), "A teammate posts while the reply is being saved"));
        // SQL forces a revision conflict during commit. InMemory covers the same ordering before commit.
        var sql = f.Db.Database.IsRelational();
        using var agentDb = f.CreateDb(sql ? new InterleavedPostInterceptor(Post) : null);
        if (!sql) f.Runner.Setup(runner => runner.RunAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(async () => { await Post(); return new RoomAgentResult("Reply preserved", []); });
        var service = f.Service(f.Owner, agentDb);
        var prompt = await service.SendAsync(room.Room.Id, new(Guid.NewGuid(), "Review the records"));
        var turn = await service.RunAgentAsync(room.Room.Id, new(Guid.NewGuid(), prompt.Id, "master-orchestrator"));
        turn.Status.Should().Be("Completed");
        using var check = f.CreateDb();
        var messages = await check.ChatRoomMessages.OrderBy(message => message.Sequence).ToListAsync();
        messages.Select(message => message.Sequence).Should().Equal(1, 2, 3, 4, 5);
        messages.Count(message => message.ActorKind == "Agent").Should().Be(1);
        var agentReply = messages.Single(message => message.ActorKind == "Agent");
        agentReply.TurnId.Should().Be(turn.Id);
        // A rolled-back reply must not leave an orphan notification event on a later save.
        var events = await check.Set<OutboxMessage>().Where(value => value.TenantId == f.Tenant && value.EventType == typeof(ChatRoomMessagePostedEvent).FullName).ToListAsync();
        events.Count(value => value.EventId == agentReply.Id).Should().Be(1);
        events.Should().OnlyContain(value => messages.Select(message => message.Id).Contains(value.EventId));
        (await check.ChatRooms.SingleAsync()).ActiveTurnId.Should().BeNull();
        f.Runner.Verify(runner => runner.RunAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }


    [Fact]
    public async Task InterruptedReplyCommit_ShouldNotLeakAReplyOrNotificationDuringRecovery()
    {
        using var f = new Fixture(); var room = await f.Create();
        await f.Service(f.Owner).AddMemberAsync(room.Room.Id, new($"human:{f.Colleague}", "Participant", true));
        using var db = f.CreateDb(new InterruptedReplyInterceptor());
        var service = f.Service(f.Owner, db);
        var prompt = await service.SendAsync(room.Room.Id, new(Guid.NewGuid(), "Review the room"));
        var turn = await service.RunAgentAsync(room.Room.Id, new(Guid.NewGuid(), prompt.Id, "master-orchestrator"));
        turn.Status.Should().Be("Unconfirmed");
        using var check = f.CreateDb();
        (await check.ChatRoomMessages.CountAsync(value => value.ActorKind == "Agent")).Should().Be(0);
        var events = await check.Set<OutboxMessage>().Where(value => value.TenantId == f.Tenant).ToListAsync();
        events.Should().ContainSingle().Which.EventId.Should().Be(prompt.Id);
        (await check.ChatRooms.SingleAsync()).ActiveTurnId.Should().BeNull();
        f.Runner.Verify(value => value.RunAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MessageNotifications_ShouldCommitOriginalAudienceAndDeduplicateMessageRetries()
    {
        using var f = new Fixture(); var room = await f.Create(); var owner = f.Service(f.Owner);
        await owner.AddMemberAsync(room.Room.Id, new($"human:{f.Colleague}", "Participant", true));
        await owner.AddMemberAsync(room.Room.Id, new($"human:{f.Observer}", "Observer", true));
        var request = new SendRoomMessageRequest(Guid.NewGuid(), "Sensitive message stays in the room", [$"human:{f.Colleague}", $"human:{f.Owner}"]);
        var message = await owner.SendAsync(room.Room.Id, request);
        await owner.SendAsync(room.Room.Id, request);
        var envelope = await f.Db.Set<OutboxMessage>().SingleAsync(value => value.TenantId == f.Tenant);
        envelope.EventId.Should().Be(message.Id);
        envelope.Payload.Should().NotContain(request.Content);
        var posted = JsonSerializer.Deserialize<ChatRoomMessagePostedEvent>(envelope.Payload, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        posted.RecipientUserIds.Should().BeEquivalentTo([f.Colleague, f.Observer]);
        var writes = new List<UserNotificationWriteRequest>(); var writer = new Mock<IUserNotificationWriter>();
        writer.Setup(value => value.WriteForUserAsync(It.IsAny<UserNotificationWriteRequest>(), It.IsAny<CancellationToken>()))
            .Callback<UserNotificationWriteRequest, CancellationToken>((value, _) => writes.Add(value)).Returns(Task.CompletedTask);
        var handler = new ChatRoomMessageNotificationHandler(f.Db, new TenantProvider(f.Tenant), f.Directory.Object, writer.Object);
        await handler.HandleAsync(posted); await handler.HandleAsync(posted);
        writes.Should().HaveCount(4);
        writes.Where(value => value.UserId == f.Colleague).Should().OnlyContain(value => value.Type == "chat.mention");
        writes.Where(value => value.UserId == f.Observer).Should().OnlyContain(value => value.Type == "chat.message");
        writes.Select(value => (value.UserId, value.IdempotencyKey)).Distinct().Should().HaveCount(2);
        writes.Should().OnlyContain(value => value.TenantId == f.Tenant && !value.SendPush && !value.Body.Contains(request.Content));
        writes[0].ActionUrl.Should().Contain($"message={message.Id:D}").And.Contain($"sequence={message.Sequence}");
    }

    [Fact]
    public async Task NotificationDelivery_ShouldRecheckMembershipStaffTenantAndOriginalAudience()
    {
        using var f = new Fixture(); var room = await f.Create(); var owner = f.Service(f.Owner);
        await owner.AddMemberAsync(room.Room.Id, new($"human:{f.Colleague}", "Participant", true));
        var message = await owner.SendAsync(room.Room.Id, new(Guid.NewGuid(), "Private room evidence"));
        var posted = new ChatRoomMessagePostedEvent(f.Tenant, room.Room.Id, message.Id, [f.Colleague], f.Now);
        await owner.RemoveMemberAsync(room.Room.Id, $"human:{f.Colleague}");
        await owner.AddMemberAsync(room.Room.Id, new($"human:{f.Observer}", "Observer", true));
        var writer = new Mock<IUserNotificationWriter>();
        var handler = new ChatRoomMessageNotificationHandler(f.Db, new TenantProvider(f.Tenant), f.Directory.Object, writer.Object);
        await handler.HandleAsync(posted);
        writer.Invocations.Should().BeEmpty();
        await owner.AddMemberAsync(room.Room.Id, new($"human:{f.Colleague}", "Participant", true));
        f.Staff.Remove(f.Colleague);
        await handler.HandleAsync(posted);
        writer.Invocations.Should().BeEmpty();
        await handler.Invoking(value => value.HandleAsync(posted with { TenantId = Guid.NewGuid() })).Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task AgentNotifications_ShouldCarryAgentPortraitIdentityAndAiRunToEveryHuman()
    {
        using var f = new Fixture(); var room = await f.Create(); var owner = f.Service(f.Owner);
        await owner.AddMemberAsync(room.Room.Id, new($"human:{f.Colleague}", "Participant", true));
        var message = await owner.SendAsync(room.Room.Id, new(Guid.NewGuid(), "Summarise the shared references"));
        var turn = await owner.RunAgentAsync(room.Room.Id, new(Guid.NewGuid(), message.Id, "master-orchestrator"));
        var reply = await f.Db.ChatRoomMessages.SingleAsync(value => value.ActorKind == "Agent");
        var envelope = await f.Db.Set<OutboxMessage>().SingleAsync(value => value.EventId == reply.Id);
        var posted = JsonSerializer.Deserialize<ChatRoomMessagePostedEvent>(envelope.Payload, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        posted.RecipientUserIds.Should().BeEquivalentTo([f.Owner, f.Colleague]);
        var writes = new List<UserNotificationWriteRequest>(); var writer = new Mock<IUserNotificationWriter>();
        writer.Setup(value => value.WriteForUserAsync(It.IsAny<UserNotificationWriteRequest>(), It.IsAny<CancellationToken>()))
            .Callback<UserNotificationWriteRequest, CancellationToken>((value, _) => writes.Add(value)).Returns(Task.CompletedTask);
        await new ChatRoomMessageNotificationHandler(f.Db, new TenantProvider(f.Tenant), f.Directory.Object, writer.Object).HandleAsync(posted);
        writes.Should().HaveCount(2).And.OnlyContain(value => value.Source == "Agent" && value.Type == "chat.message" && value.AiRunId == turn.AiRunId);
        writes[0].MetadataJson.Should().Contain("master-orchestrator").And.Contain("AONIK");
    }

    [Fact]
    public async Task MutationGuard_ShouldRefuseAfterMembershipRevocation()
    {
        using var f = new Fixture(); var room = await f.Create();
        var entity = await f.Db.ChatRooms.SingleAsync(); var turnId = Guid.NewGuid(); entity.ActiveTurnId = turnId; await f.Db.SaveChangesAsync();
        var guard = new ChatRoomInvocationGuard(f.Provider.GetRequiredService<IServiceScopeFactory>()) { RoomId = room.Room.Id, TenantId = f.Tenant, UserId = f.Owner, AgentName = "master-orchestrator", TurnId = turnId };
        await guard.ValidateAsync();
        var agent = await f.Db.ChatRoomMembers.SingleAsync(member => member.AgentName != null); agent.IsActive = false; await f.Db.SaveChangesAsync();
        await guard.Invoking(value => value.ValidateAsync()).Should().ThrowAsync<ChatRoomException>().Where(error => error.StatusCode == 403);
    }
}
