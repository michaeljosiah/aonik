using Aonik.Agents.Framework;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Agents.Approval;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Xunit;

namespace Aonik.Application.Tests.Agents.Tenant;

/// <summary>
/// Spec 033 §8.5 — the spine. A tenant tool whose classification is registered in the request-scoped
/// store must flow through the SAME Spec 032 gate as a built-in: read-only passes through, mutating is
/// wrapped so it cannot run ungated, and a mutating-looking tool with no classification throws at gate
/// time (fail closed). This mirrors the real <c>TenantToolApprovalManifest</c> by backing the manifest
/// with the same <see cref="ITenantToolClassificationStore"/> the providers populate.
/// </summary>
public sealed class TenantToolGateSpineTests
{
    private sealed class StoreBackedTenantManifest : IToolApprovalManifest
    {
        private readonly ITenantToolClassificationStore _store;
        public StoreBackedTenantManifest(ITenantToolClassificationStore store) => _store = store;
        public string Module => "Tenant";
        public ToolClassification? Classify(string toolName) => _store.Find(toolName);
    }

    private sealed class NoopAuditSink : IToolApprovalAuditSink
    {
        public void Record(ToolApprovalAuditEntry entry) { }
    }

    private static (ToolApprovalGate Gate, ITenantToolClassificationStore Store) BuildGate()
    {
        var store = new TenantToolClassificationStore();
        var gate = new ToolApprovalGate(new IToolApprovalManifest[] { new StoreBackedTenantManifest(store) }, new NoopAuditSink());
        return (gate, store);
    }

    [Fact]
    public void Gate_Should_PassThrough_ReadOnly_TenantTool_Unchanged()
    {
        var (gate, store) = BuildGate();
        store.Register("tenant_lookup_customer", ToolClassification.ReadOnly);
        var tool = AIFunctionFactory.Create(() => "ok", "tenant_lookup_customer");

        var gated = gate.Gate(tool);

        gated.Should().BeSameAs(tool);
    }

    [Fact]
    public void Gate_Should_Wrap_Mutating_TenantTool()
    {
        var (gate, store) = BuildGate();
        store.Register("tenant_create_widget", ToolClassification.Mutating(
            new ToolApprovalOptions(ToolApprovalTier.High, "Create widget", "Tenant.Http.create_widget")));
        var tool = AIFunctionFactory.Create(() => "ok", "tenant_create_widget");

        var gated = gate.Gate(tool, serviceProvider: null);

        gated.Should().NotBeSameAs(tool);
        gated.Should().BeAssignableTo<AIFunction>();
        gated.GetType().Name.Should().Be("ApprovalGatedAIFunction");
    }

    [Fact]
    public void Gate_Should_FailClosed_On_Unclassified_Mutating_TenantTool()
    {
        var (gate, _) = BuildGate(); // nothing registered → store returns null → fail-closed default
        var tool = AIFunctionFactory.Create(() => "ok", "tenant_delete_record");

        var act = () => gate.Gate(tool);

        act.Should().Throw<ToolNotClassifiedException>()
            .Which.ToolName.Should().Be("tenant_delete_record");
    }

    [Fact]
    public void Gate_Should_PassThrough_Unclassified_ReadLooking_TenantTool()
    {
        var (gate, _) = BuildGate(); // not registered, but the name does not look mutating
        var tool = AIFunctionFactory.Create(() => "ok", "tenant_list_things");

        var gated = gate.Gate(tool);

        gated.Should().BeSameAs(tool);
    }
}
