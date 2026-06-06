using Aonik.Finance.Agents;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Xunit;

namespace Aonik.Application.Tests.Agents.Tenant;

/// <summary>
/// Spec 033 — the IDomainAgentDescriptor allow-list contract ("a non-null whitelist includes only
/// those names") must apply to tenant-contributed tools too, so a restricted build (voice read-only,
/// playground with tools disabled, or a tenant toolset override) can never surface an unlisted
/// tenant tool.
/// </summary>
public sealed class FinanceAgentToolAllowListTests
{
    private static AITool Tool(string name) => AIFunctionFactory.Create(() => "ok", name);

    [Fact]
    public void NullAllowList_KeepsEverything_IncludingTenantTools()
    {
        var tools = new[] { Tool("finance_create_invoice"), Tool("tenant_post_order") };

        var result = FinanceAgentDescriptor.ApplyToolAllowList(tools, null).Select(t => t.Name).ToList();

        result.Should().BeEquivalentTo("finance_create_invoice", "tenant_post_order");
    }

    [Fact]
    public void AllowList_Excludes_TenantTool_NotOnTheList()
    {
        var tools = new[] { Tool("finance_create_invoice"), Tool("tenant_post_order") };
        var allow = new HashSet<string>(StringComparer.Ordinal) { "finance_create_invoice" };

        var result = FinanceAgentDescriptor.ApplyToolAllowList(tools, allow).Select(t => t.Name).ToList();

        result.Should().ContainSingle().Which.Should().Be("finance_create_invoice");
        result.Should().NotContain("tenant_post_order");
    }

    [Fact]
    public void AllowList_Keeps_TenantTool_WhenExplicitlyWhitelisted()
    {
        var tools = new[] { Tool("tenant_post_order") };
        var allow = new HashSet<string>(StringComparer.Ordinal) { "tenant_post_order" };

        FinanceAgentDescriptor.ApplyToolAllowList(tools, allow).Select(t => t.Name)
            .Should().ContainSingle().Which.Should().Be("tenant_post_order");
    }

    [Fact]
    public void EmptyAllowList_FiltersEverything_IncludingTenantTools()
    {
        // The read-only / tools-disabled case: an empty whitelist must yield no tools at all.
        var tools = new[] { Tool("finance_create_invoice"), Tool("tenant_post_order") };
        var allow = (IReadOnlySet<string>)new HashSet<string>(StringComparer.Ordinal);

        FinanceAgentDescriptor.ApplyToolAllowList(tools, allow).Should().BeEmpty();
    }
}
