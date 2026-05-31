using Aonik.Finance.Agents;
using Aonik.SharedKernel.Abstractions.Agents;

using FluentAssertions;

namespace Aonik.Application.Tests.Agents.Approval;

/// <summary>
/// Spec 032 (finding C3) — the Finance module's tool-approval classification. Pins the tier of
/// each mutating finance tool (money movement = High, everyday writes = Medium) and confirms
/// read tools are left unclassified so the gate passes them through. A lint-style trip-wire
/// asserts no tool that looks like a mutation is ever classified read-only.
/// </summary>
public class FinanceToolApprovalManifestTests
{
    private static readonly IToolApprovalManifest Manifest = new FinanceToolApprovalManifest();

    public static IEnumerable<object[]> HighTierTools() => new[]
    {
        new object[] { "finance_capture_payment", "Finance.CapturePayment" },
        new object[] { "finance_cancel_payment", "Finance.CancelPayment" },
        new object[] { "finance_create_payment_intent", "Finance.CreatePaymentIntent" },
        new object[] { "finance_mark_invoice_paid", "Finance.MarkInvoicePaid" },
    };

    public static IEnumerable<object[]> MediumTierTools() => new[]
    {
        new object[] { "finance_create_invoice" },
        new object[] { "finance_issue_invoice" },
        new object[] { "finance_cancel_invoice" },
        new object[] { "finance_add_invoice_line" },
        new object[] { "finance_update_line_quantity" },
        new object[] { "finance_update_line_unit_price" },
        new object[] { "finance_apply_invoice_discount" },
        new object[] { "finance_create_ledger" },
        new object[] { "finance_create_account" },
    };

    public static IEnumerable<object[]> ReadTools() => new[]
    {
        new object[] { "finance_get_invoice" },
        new object[] { "finance_list_ledgers" },
        new object[] { "finance_list_accounts" },
        new object[] { "finance_graph_get_schema" },
        new object[] { "finance_graph_get_account_statement" },
    };

    [Fact]
    public void Module_Should_BeFinance()
    {
        Manifest.Module.Should().Be("Finance");
    }

    [Theory]
    [MemberData(nameof(HighTierTools))]
    public void Classify_Should_ReturnHighMutating_When_MoneyMovementTool(string toolName, string proposalType)
    {
        var classification = Manifest.Classify(toolName);

        classification.Should().NotBeNull();
        classification!.IsMutating.Should().BeTrue();
        classification.Options.Should().NotBeNull();
        classification.Options!.Tier.Should().Be(ToolApprovalTier.High);
        classification.Options.ProposalType.Should().Be(proposalType,
            "High-tier money tools carry the durable proposal type they will be marshalled into");
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
    }

    [Theory]
    [MemberData(nameof(ReadTools))]
    public void Classify_Should_ReturnNull_When_ReadTool(string toolName)
    {
        Manifest.Classify(toolName).Should().BeNull(
            "read tools are left unclassified so the gate passes them through without ceremony");
    }

    [Fact]
    public void EveryClassifiedTool_Should_BeMutating_And_NeverReadLookingMislabelled()
    {
        var allMutating = HighTierTools().Select(a => (string)a[0])
            .Concat(MediumTierTools().Select(a => (string)a[0]));

        foreach (var toolName in allMutating)
        {
            var classification = Manifest.Classify(toolName);
            classification.Should().NotBeNull($"{toolName} is a mutation and must be classified");
            classification!.IsReadOnly.Should().BeFalse($"{toolName} must never be classified read-only");

            // Trip-wire: a tool the manifest claims as a mutation must also look like one to the
            // name heuristic, so the gate's fail-closed default and this manifest stay in agreement.
            MutatingToolNameHeuristic.LooksMutating(toolName).Should().BeTrue(
                $"{toolName} is classified mutating, so the name heuristic must also flag it");
        }
    }
}
