using Aonik.Finance.Agents;
using Aonik.SharedKernel.Abstractions.Agents;

using FluentAssertions;

namespace Aonik.Application.Tests.Agents.Approval;

/// <summary>
/// Spec 032 — the PersonalFinance ("Simi") tool-approval classification. Pins the tier of each
/// mutating <c>pf_*</c> / <c>user_memory_save</c> tool (everyday writes = Medium, reversible
/// personal-state = Low; PersonalFinance moves no money, so there is no High tier here) and confirms
/// read tools are left unclassified so the gate passes them through. A lint-style trip-wire asserts
/// every classified mutation also trips the (widened) name heuristic, keeping the manifest and the
/// gate's fail-closed default in agreement.
/// </summary>
public class PersonalFinanceToolApprovalManifestTests
{
    private static readonly IToolApprovalManifest Manifest = new PersonalFinanceToolApprovalManifest();

    public static IEnumerable<object[]> LowTierTools() => new[]
    {
        new object[] { "user_memory_save" },
        new object[] { "pf_reject_commitment" },
    };

    public static IEnumerable<object[]> MediumTierTools() => new[]
    {
        new object[] { "pf_create_account" },
        new object[] { "pf_archive_account" },
        new object[] { "pf_create_transaction" },
        new object[] { "pf_create_bill" },
        new object[] { "pf_update_bill" },
        new object[] { "pf_archive_bill" },
        new object[] { "pf_create_budget" },
        new object[] { "pf_update_budget_amount" },
        new object[] { "pf_delete_budget" },
        new object[] { "pf_create_commitment_from_transaction" },
        new object[] { "pf_confirm_commitment" },
        new object[] { "pf_override_transaction_category" },
        new object[] { "pf_create_categorisation_rule" },
        new object[] { "pf_apply_statement_import" },
        new object[] { "pf_delete_transaction_attachment" },
        new object[] { "pf_cancel_order" },
        new object[] { "pf_create_account_link_session" },
        new object[] { "pf_refresh_linked_account" },
        new object[] { "pf_sync_linked_account_transactions" },
        new object[] { "pf_disconnect_linked_account" },
        // Spec 021 (AONIK Compass) — guidance writes, no money movement.
        new object[] { "pf_create_goal_programme" },
        new object[] { "pf_update_goal_programme" },
        new object[] { "pf_generate_goal_plan" },
        new object[] { "pf_create_compass_proposal" },
    };

    public static IEnumerable<object[]> ReadTools() => new[]
    {
        new object[] { "pf_list_accounts" },
        new object[] { "pf_get_account" },
        new object[] { "pf_list_transactions" },
        new object[] { "pf_get_spending_summary" },
        new object[] { "pf_run_insights" },
        new object[] { "pf_compare_snapshots" },
        new object[] { "user_memory_recall" },
        // Spec 021 (AONIK Compass) — read/guidance tools must pass through ungated.
        new object[] { "pf_list_goals" },
        new object[] { "pf_get_goal" },
        new object[] { "pf_get_goal_plan" },
        new object[] { "pf_get_safe_to_spend" },
        new object[] { "pf_run_compass_planner" },
    };

    [Fact]
    public void Module_Should_BePersonalFinance()
    {
        Manifest.Module.Should().Be("PersonalFinance");
    }

    [Theory]
    [MemberData(nameof(LowTierTools))]
    public void Classify_Should_ReturnLowMutating_When_ReversiblePersonalStateWrite(string toolName)
    {
        var classification = Manifest.Classify(toolName);

        classification.Should().NotBeNull();
        classification!.IsMutating.Should().BeTrue();
        classification.Options.Should().NotBeNull();
        classification.Options!.Tier.Should().Be(ToolApprovalTier.Low);
        classification.Options.ProposalType.Should().BeNull("PersonalFinance has no High / proposal-backed tools");
    }

    [Theory]
    [MemberData(nameof(MediumTierTools))]
    public void Classify_Should_ReturnMediumMutating_When_EverydayDomainWrite(string toolName)
    {
        var classification = Manifest.Classify(toolName);

        classification.Should().NotBeNull();
        classification!.IsMutating.Should().BeTrue();
        classification.Options.Should().NotBeNull();
        classification.Options!.Tier.Should().Be(ToolApprovalTier.Medium);
        classification.Options.ProposalType.Should().BeNull("PersonalFinance moves no money — no High / proposal tools");
    }

    [Theory]
    [MemberData(nameof(ReadTools))]
    public void Classify_Should_ReturnNull_When_ReadTool(string toolName)
    {
        Manifest.Classify(toolName).Should().BeNull(
            "read tools are left unclassified so the gate passes them through without ceremony");
    }

    [Fact]
    public void EveryClassifiedTool_Should_BeMutating_And_TripTheNameHeuristic()
    {
        var allMutating = LowTierTools().Select(a => (string)a[0])
            .Concat(MediumTierTools().Select(a => (string)a[0]));

        foreach (var toolName in allMutating)
        {
            var classification = Manifest.Classify(toolName);
            classification.Should().NotBeNull($"{toolName} is a mutation and must be classified");
            classification!.IsReadOnly.Should().BeFalse($"{toolName} must never be classified read-only");

            // Trip-wire: a tool the manifest claims as a mutation must also look like one to the name
            // heuristic, so an accidental de-classification still fails closed at the gate.
            MutatingToolNameHeuristic.LooksMutating(toolName).Should().BeTrue(
                $"{toolName} is classified mutating, so the (widened) name heuristic must also flag it");
        }
    }
}
