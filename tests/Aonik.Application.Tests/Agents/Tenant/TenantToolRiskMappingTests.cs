using Aonik.Agents.Entities;
using Aonik.Agents.Framework;
using Aonik.SharedKernel.Abstractions.Agents;
using FluentAssertions;
using Xunit;

namespace Aonik.Application.Tests.Agents.Tenant;

/// <summary>
/// Spec 033 §8.5 — tenant tools default to High and never self-downgrade. These cover the pure
/// mapping from a persisted <see cref="TenantToolRiskTier"/> to the Spec 032
/// <see cref="ToolClassification"/> the gate consumes.
/// </summary>
public sealed class TenantToolRiskMappingTests
{
    [Fact]
    public void ClassifyHttpTool_Should_BeReadOnly_When_RiskTierReadOnly()
    {
        var tool = new TenantHttpTool { Name = "tenant_lookup", RiskTier = TenantToolRiskTier.ReadOnly };

        var classification = TenantToolRiskMapping.ClassifyHttpTool(tool);

        classification.IsReadOnly.Should().BeTrue();
    }

    [Fact]
    public void ClassifyHttpTool_Should_DefaultHigh_When_Mutating()
    {
        var tool = new TenantHttpTool { Name = "tenant_post_order", RiskTier = TenantToolRiskTier.High, ActionKind = "Post order" };

        var classification = TenantToolRiskMapping.ClassifyHttpTool(tool);

        classification.IsMutating.Should().BeTrue();
        classification.Options!.Tier.Should().Be(ToolApprovalTier.High);
        classification.Options.ActionKind.Should().Be("Post order");
    }

    [Theory]
    [InlineData(TenantToolRiskTier.Low, ToolApprovalTier.Low)]
    [InlineData(TenantToolRiskTier.Medium, ToolApprovalTier.Medium)]
    [InlineData(TenantToolRiskTier.High, ToolApprovalTier.High)]
    public void ClassifyHttpTool_Should_MapLoweredTiers(TenantToolRiskTier rowTier, ToolApprovalTier expected)
    {
        var tool = new TenantHttpTool { Name = "tenant_update_x", RiskTier = rowTier };

        var classification = TenantToolRiskMapping.ClassifyHttpTool(tool);

        classification.Options!.Tier.Should().Be(expected);
    }

    [Theory]
    // A read-LOOKING name on a mutating-default server must still be gated — the remote's name is not
    // trusted (fail closed). Includes side-effecting names a verb heuristic would miss.
    [InlineData("acme_get_quote")]
    [InlineData("send_invoice")]
    [InlineData("charge_card")]
    [InlineData("email_customer")]
    [InlineData("anything")]
    public void ClassifyMcpTool_Should_InheritServerMutatingTier_RegardlessOfName(string toolName)
    {
        var server = new TenantMcpServer { Name = "acme", DefaultRiskTier = TenantToolRiskTier.High };

        var classification = TenantToolRiskMapping.ClassifyMcpTool(toolName, server);

        classification.IsMutating.Should().BeTrue("a tenant MCP tool is never assumed read-only from its name");
        classification.Options!.Tier.Should().Be(ToolApprovalTier.High);
    }

    [Fact]
    public void ClassifyMcpTool_Should_InheritServerTier_When_NameLooksMutating()
    {
        var server = new TenantMcpServer { Name = "acme", DefaultRiskTier = TenantToolRiskTier.High };

        var classification = TenantToolRiskMapping.ClassifyMcpTool("acme_create_widget", server);

        classification.IsMutating.Should().BeTrue();
        classification.Options!.Tier.Should().Be(ToolApprovalTier.High);
    }

    [Fact]
    public void ClassifyMcpTool_Should_InheritLoweredServerTier_RegardlessOfName()
    {
        // A PlatformAdmin lowered the server to Medium → all its tools are Medium (still gated), even
        // read-looking ones. Read-only requires the explicit ReadOnly server tier.
        var server = new TenantMcpServer { Name = "acme", DefaultRiskTier = TenantToolRiskTier.Medium };

        var classification = TenantToolRiskMapping.ClassifyMcpTool("acme_get_quote", server);

        classification.IsMutating.Should().BeTrue();
        classification.Options!.Tier.Should().Be(ToolApprovalTier.Medium);
    }

    [Fact]
    public void ClassifyMcpTool_Should_BeReadOnly_When_ServerDefaultReadOnly_EvenForMutatingName()
    {
        // A PlatformAdmin set the whole server read-only — they vouched its tools are safe.
        var server = new TenantMcpServer { Name = "acme", DefaultRiskTier = TenantToolRiskTier.ReadOnly };

        var classification = TenantToolRiskMapping.ClassifyMcpTool("acme_create_widget", server);

        classification.IsReadOnly.Should().BeTrue();
    }
}
