using System.Text.Json;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;

using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Entities;
using Aonik.Agents.Persistence;
using Aonik.Agents.Services;
using Aonik.Agents.Services.Connectors;
using Aonik.Agents.Services.Workflows;
using Aonik.IntegrationTests.Support;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

namespace Aonik.Database.Tests;

public sealed class ConnectorMutationSqlServerTests(SqlLocalDbFixture database) : IClassFixture<SqlLocalDbFixture>
{
    [SkippableTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentPreparation_Should_CommitOneApprovalAndOnlyItsCorrelatedProposal(bool high)
    {
        Skip.IfNot(database.IsAvailable, database.SkipReason);
        Guid tenantId = Guid.NewGuid(), userId = Guid.NewGuid(), connectionId = Guid.NewGuid();
        string key = high ? GmailActionKeys.SendMessage : GmailActionKeys.CreateDraft;
        var action = new ConnectorActionRef(key, 1);
        JsonElement args = ConnectorJson.Element(new GmailSendRequest("same-operation", "protected", new string('a', 64)));
        var context = new ToolGateContext(GmailActionPolicies.ToolNames[key], GmailActionPolicies.Approval(key)!, ConnectorMutationArguments.ToolArguments(connectionId, args),
            new("Review message", null, ConnectorJson.Element(new { targetCount = 1 })), ConnectorInvocationIdentity.Key(tenantId, userId, connectionId, action, "same-operation"));
        Task<ToolGateOutcome?>[] submissions = Enumerable.Range(0, 8).Select(async _ =>
        {
            await using AgentsDbContext db = Context(tenantId);
            try { return await Gate(db, tenantId, userId).GateAsync(context); }
            catch (WorkflowConflictException) { return null; }
        }).ToArray();

        ToolGateOutcome?[] results = await Task.WhenAll(submissions);

        await using AgentsDbContext verify = Context(tenantId);
        ToolApprovalRequest request = await verify.ToolApprovalRequests.SingleAsync();
        request.InvocationKey.Should().Be(context.InvocationKey);
        results.Where(result => result is not null).Select(result => result!.ApprovalRequestId).Distinct().Should().Equal(request.Id);
        (await verify.Proposals.CountAsync()).Should().Be(high ? 1 : 0);
        if (high) (await verify.Proposals.SingleAsync()).Id.Should().Be(request.ProposalId!.Value);
        (await Gate(verify, tenantId, userId).GateAsync(context)).ApprovalRequestId.Should().Be(request.Id);
    }

    [SkippableFact]
    public async Task ConcurrentDispatchClaims_Should_HaveOneWinner_AndFreshContextShouldReturnTheSameReceipt()
    {
        Skip.IfNot(database.IsAvailable, database.SkipReason);
        Guid tenantId = Guid.NewGuid(), userId = Guid.NewGuid(), connectionId = Guid.NewGuid();
        var action = new ConnectorActionRef(GmailActionKeys.Archive, 1);
        JsonElement args = ConnectorJson.Element(new GmailMessageTargets("archive-once", ["message-1"]));
        Guid requestId;
        await using (AgentsDbContext seed = Context(tenantId))
        {
            var request = new ToolApprovalRequest { TenantId = tenantId, RequestingUserId = userId, DecidedByUserId = userId, ToolName = GmailActionPolicies.ToolNames[action.Key],
                RiskTier = "Medium", ArgsHash = ConnectorMutationArguments.Hash(connectionId, args), ArgumentsRedactedJson = ConnectorJson.Serialize(ConnectorMutationArguments.ToolArguments(connectionId, args)),
                InvocationKey = ConnectorInvocationIdentity.Key(tenantId, userId, connectionId, action, "archive-once"), Status = ToolApprovalRequestStatus.Approved,
                PreviewJson = ConnectorJson.Serialize(new ToolApprovalPreview("Archive reviewed messages", null, ConnectorJson.Element(new { connectionId, providerAccountId = "provider-account" }))),
                RequestedAt = DateTime.UtcNow.AddMinutes(-1), DecidedAt = DateTime.UtcNow, ConsumedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddMinutes(10) };
            seed.ToolApprovalRequests.Add(request); await seed.SaveChangesAsync(); requestId = request.Id;
        }
        var input = new ConnectorMutationExecutionRequest(connectionId, "provider-account", action, args);
        Task<ConnectorMutationClaim?>[] attempts = Enumerable.Range(0, 8).Select(async _ =>
        {
            await using AgentsDbContext db = Context(tenantId);
            try { return await Authority(db, tenantId, userId).ClaimAsync(input); }
            catch (WorkflowConflictException) { return null; }
        }).ToArray();

        ConnectorMutationClaim?[] claims = await Task.WhenAll(attempts);

        ConnectorMutationClaim winner = claims.Where(claim => claim?.Status == "Claimed").Should().ContainSingle().Which!;
        winner.OperationId.Should().Be(requestId);
        JsonElement receipt = ConnectorJson.Element(new GmailMutationReceipt(action.Key, requestId, "Completed", ["message-1"]));
        await using (AgentsDbContext complete = Context(tenantId)) await Authority(complete, tenantId, userId).CompleteAsync(requestId, winner.LeaseId, receipt);
        await using AgentsDbContext restarted = Context(tenantId);
        ConnectorMutationClaim replay = await Authority(restarted, tenantId, userId).ClaimAsync(input);
        replay.Status.Should().Be("Completed"); replay.Result!.Value.GetRawText().Should().Be(receipt.GetRawText());
        var gateContext = new ToolGateContext(GmailActionPolicies.ToolNames[action.Key], GmailActionPolicies.Approval(action.Key)!, ConnectorMutationArguments.ToolArguments(connectionId, args),
            InvocationKey: ConnectorInvocationIdentity.Key(tenantId, userId, connectionId, action, "archive-once"));
        (await Gate(restarted, tenantId, userId).GateAsync(gateContext)).Decision.Should().Be(ToolGateDecision.Completed);
    }

    private AgentsDbContext Context(Guid tenantId) => new(database.CreateOptions<AgentsDbContext>(), new TestTenantProvider(tenantId));
    private static ToolApprovalService Gate(AgentsDbContext db, Guid tenantId, Guid userId) => new(new ToolApprovalRequestStore(db), Mock.Of<IAgentProposalStore>(),
        Mock.Of<IProposalApprovalService>(), new ProposalApprovalPolicy(), new TestTenantProvider(tenantId), new TestCurrentUserProvider(userId), new Clock(), workflowDatabase: db);
    private static ConnectorMutationAuthority Authority(AgentsDbContext db, Guid tenantId, Guid userId)
    {
        var tenant = new TestTenantProvider(tenantId); var user = new TestCurrentUserProvider(userId); var clock = new Clock(); var permissions = new AllowAllPermissionService();
        var users = new Mock<IUserDirectoryReader>();
        users.Setup(reader => reader.GetByIdsAsync(tenantId, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new[] { new UserDirectoryItem(userId, null, "Active") });
        var settings = Mock.Of<ITenantSettingStore>();
        var execution = new WorkflowExecutionAuthorization(tenant, permissions, users.Object, settings, new ConfigurationBuilder().Build(), []);
        var reviewers = new WorkflowReviewerAuthorization(db, tenant, users.Object, Mock.Of<ITenantRoleReader>(), permissions, execution, clock);
        return new(db, tenant, user, users.Object, permissions, clock, new WorkflowInvocationContext(), Mock.Of<IWorkflowConnectorContextReader>(), reviewers, execution);
    }
    private sealed class Clock : IClock { public DateTime UtcNow => DateTime.UtcNow; }
}
