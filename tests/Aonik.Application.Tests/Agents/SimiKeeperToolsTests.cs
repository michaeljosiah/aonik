using Aonik.Finance.Agents;
using FluentAssertions;
using Xunit;

namespace Aonik.Application.Tests.Agents;

/// <summary>
/// Spec 047 §9 — the Keeper is a read-only configuration over the Simi
/// aggregates. These tests pin the two structural guarantees that make the fence
/// real regardless of the prompt: the <c>simi_*</c> reads are omitted from the
/// approval manifest (so the gate passes them through, exactly as the existing
/// <c>pf_get_*</c>/<c>pf_list_*</c> reads), and the agent carries the
/// describe-never-prescribe Keeper instructions.
/// </summary>
public sealed class SimiKeeperToolsTests
{
    public static readonly TheoryData<string> KeeperToolNames = new()
    {
        "simi_list_care_entities",
        "simi_get_entity_profile",
        "simi_list_payment_logs",
        "simi_year_summary",
        "simi_list_commitment_cycles",
    };

    [Theory]
    [MemberData(nameof(KeeperToolNames))]
    public void KeeperReadTool_Should_BeUnclassified_So_TheGatePassesItUngated(string toolName)
    {
        var manifest = new PersonalFinanceToolApprovalManifest();

        manifest.Classify(toolName)
            .Should().BeNull("read-only Keeper tools must pass the approval gate unwrapped");
    }

    [Fact]
    public void PersonalFinanceAgent_Should_CarryTheDescribeNeverPrescribeKeeperFence()
    {
        PersonalFinanceAgentDescriptor.Instructions.Should().Contain("Describe, never prescribe");
        PersonalFinanceAgentDescriptor.Instructions.Should().Contain("simi_year_summary");
        PersonalFinanceAgentDescriptor.Instructions.Should().Contain("Refuse advice");
    }
}
