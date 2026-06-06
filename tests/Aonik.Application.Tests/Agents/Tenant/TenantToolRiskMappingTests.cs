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

    [Fact]
    public void ClassifyMcpTool_Should_BeReadOnly_When_NameLooksReadOnly()
    {
        var server = new TenantMcpServer { Name = "acme", DefaultRiskTier = TenantToolRiskTier.High };

        var classification = TenantToolRiskMapping.ClassifyMcpTool("acme_get_quote", server);

        classification.IsReadOnly.Should().BeTrue("a read-looking discovered tool passes through even on a High-default server");
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
    public void ClassifyMcpTool_Should_BeReadOnly_When_ServerDefaultReadOnly_EvenForMutatingName()
    {
        // A PlatformAdmin set the whole server read-only — they vouched its tools are safe.
        var server = new TenantMcpServer { Name = "acme", DefaultRiskTier = TenantToolRiskTier.ReadOnly };

        var classification = TenantToolRiskMapping.ClassifyMcpTool("acme_create_widget", server);

        classification.IsReadOnly.Should().BeTrue();
    }
}
