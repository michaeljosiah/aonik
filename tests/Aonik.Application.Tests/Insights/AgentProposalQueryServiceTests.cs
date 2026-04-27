using Aonik.Agents.Entities;
using Aonik.Agents.Persistence;
using Aonik.Agents.Services.Insights;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Insights;

/// <summary>
/// Focused coverage of AgentProposalQueryService reading the real
/// Proposal.Confidence column (Wave 4c.1) instead of deriving from
/// RiskTier as it did before the column existed.
/// </summary>
public class AgentProposalQueryServiceTests
{
    private sealed class TestTenantProvider(Guid tenantId) : ITenantProvider
    {
        public Guid GetCurrentTenantId() => tenantId;
        public bool TryGetCurrentTenantId(out Guid id) { id = tenantId; return true; }
    }

    private static AgentsDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AgentsDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;
        return new AgentsDbContext(options, new TestTenantProvider(tenantId));
    }

    [Fact]
    public async Task ListPendingAsync_Should_ReturnConfidenceFromColumn_When_ProposalsExist()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDbContext(tenantId);

        var agent = new Agent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Billing",
            Domain = "Billing",
            Description = "Billing agent",
            InstructionsText = string.Empty,
            ToolsetIdsJson = "[]",
            InputSchemaJson = "{}",
            OutputSchemaJson = "{}",
            PermissionsProfileJson = "{}",
            RiskTier = "Low",
            IsActive = true,
        };
        db.Agents.Add(agent);

        db.Proposals.AddRange(
            new Proposal
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProposalType = "InvoiceMatch",
                ProposedByAgentId = agent.Id,
                AiRunId = Guid.NewGuid(),
                ImpactSummary = "Match INV-2041 to bank txn",
                RiskTier = "Low",
                Confidence = 0.97m,
                Status = ProposalStatus.Proposed,
                PayloadJson = "{}",
                CreatedAt = DateTime.UtcNow,
            },
            new Proposal
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProposalType = "JournalAccrual",
                ProposedByAgentId = agent.Id,
                AiRunId = Guid.NewGuid(),
                ImpactSummary = "Accrue Apr rent",
                RiskTier = "Medium",
                Confidence = 0.62m,
                Status = ProposalStatus.Proposed,
                PayloadJson = "{}",
                CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            });
        await db.SaveChangesAsync();

        var service = new AgentProposalQueryService(db);

        var result = await service.ListPendingAsync(take: 5);

        result.Should().HaveCount(2);
        result[0].Confidence.Should().Be(0.97m,
            "newer proposal sorts first and its persisted Confidence column wins over any RiskTier derivation");
        result[1].Confidence.Should().Be(0.62m,
            "the second proposal also reads its persisted Confidence even though RiskTier=Medium would have mapped to 0.85");
    }

    [Fact]
    public async Task ListPendingAsync_Should_OnlyReturnProposed_When_StatusVaries()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDbContext(tenantId);

        var agentId = Guid.NewGuid();
        db.Agents.Add(new Agent
        {
            Id = agentId,
            TenantId = tenantId,
            Name = "Ledger",
            Domain = "Ledger",
            Description = string.Empty,
            InstructionsText = string.Empty,
            ToolsetIdsJson = "[]",
            InputSchemaJson = "{}",
            OutputSchemaJson = "{}",
            PermissionsProfileJson = "{}",
            RiskTier = "Low",
            IsActive = true,
        });

        db.Proposals.AddRange(
            new Proposal
            {
                Id = Guid.NewGuid(), TenantId = tenantId, ProposedByAgentId = agentId,
                AiRunId = Guid.NewGuid(), ProposalType = "T", ImpactSummary = "pending",
                RiskTier = "Low", Confidence = 0.9m, Status = ProposalStatus.Proposed,
                PayloadJson = "{}", CreatedAt = DateTime.UtcNow,
            },
            new Proposal
            {
                Id = Guid.NewGuid(), TenantId = tenantId, ProposedByAgentId = agentId,
                AiRunId = Guid.NewGuid(), ProposalType = "T", ImpactSummary = "approved",
                RiskTier = "Low", Confidence = 0.9m, Status = ProposalStatus.Approved,
                PayloadJson = "{}", CreatedAt = DateTime.UtcNow,
            },
            new Proposal
            {
                Id = Guid.NewGuid(), TenantId = tenantId, ProposedByAgentId = agentId,
                AiRunId = Guid.NewGuid(), ProposalType = "T", ImpactSummary = "rejected",
                RiskTier = "Low", Confidence = 0.9m, Status = ProposalStatus.Rejected,
                PayloadJson = "{}", CreatedAt = DateTime.UtcNow,
            });
        await db.SaveChangesAsync();

        var service = new AgentProposalQueryService(db);
        var result = await service.ListPendingAsync();

        result.Should().HaveCount(1);
        result[0].Summary.Should().Be("pending");
    }
}
