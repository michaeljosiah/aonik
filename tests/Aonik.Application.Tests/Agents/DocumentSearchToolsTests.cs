namespace Aonik.Application.Tests.Agents;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Agents.Tools;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

/// <summary>
/// The scoped document-search agent tool (Spec 035 §13). The security-critical behaviour is that the
/// retrieval scope (tenant + owner party) is derived from authenticated context and never from model
/// input (R7), so these assert the scope passed to <see cref="IDocumentSearch"/> rather than the
/// model-facing return shape.
/// </summary>
public sealed class DocumentSearchToolsTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _partyId = Guid.NewGuid();

    private readonly Mock<IDocumentSearch> _search = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly Mock<ICurrentUserProvider> _currentUserProvider = new();
    private readonly Mock<IUserPartyResolver> _partyResolver = new();

    public DocumentSearchToolsTests()
    {
        var tenantOut = _tenantId;
        _tenantProvider.Setup(p => p.TryGetCurrentTenantId(out tenantOut)).Returns(true);
        var userOut = _userId;
        _currentUserProvider.Setup(p => p.TryGetCurrentUserId(out userOut)).Returns(true);
        _partyResolver
            .Setup(r => r.GetPartyIdForUserAsync(_tenantId, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_partyId);
    }

    private AIFunction CreateTool()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_search.Object);
        services.AddSingleton(_tenantProvider.Object);
        services.AddSingleton(_currentUserProvider.Object);
        services.AddSingleton(_partyResolver.Object);
        var provider = services.BuildServiceProvider();
        return (AIFunction)DocumentSearchTools.CreateAll(provider).Single();
    }

    [Fact]
    public async Task SearchMyDocuments_Should_Scope_To_Authenticated_Tenant_And_Resolved_Party()
    {
        DocumentSearchScope? captured = null;
        _search
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(), It.IsAny<DocumentSearchScope>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<string, DocumentSearchScope, int, CancellationToken>((_, scope, _, _) => captured = scope)
            .ReturnsAsync(new List<DocumentChunkHit>());

        var tool = CreateTool();
        await tool.InvokeAsync(
            new AIFunctionArguments { ["query"] = "my tax return", ["limit"] = 5 }, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.TenantId.Should().Be(_tenantId);
        captured.OwnerPartyId.Should().Be(_partyId, "owner party is derived from auth context, not model input");
    }

    [Fact]
    public async Task SearchMyDocuments_Should_Clamp_Limit_To_Twenty()
    {
        _search
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(), It.IsAny<DocumentSearchScope>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DocumentChunkHit>
            {
                new(Guid.NewGuid(), 2, "tax content", 0.9, "tax_return", _partyId),
            });

        var tool = CreateTool();
        await tool.InvokeAsync(
            new AIFunctionArguments { ["query"] = "tax", ["limit"] = 999 }, CancellationToken.None);

        _search.Verify(
            s => s.SearchAsync("tax", It.IsAny<DocumentSearchScope>(), 20, It.IsAny<CancellationToken>()),
            Times.Once, "an over-large limit is clamped to the cap");
    }

    [Fact]
    public async Task SearchMyDocuments_Should_Fail_Closed_When_No_Authenticated_Tenant()
    {
        var noTenant = Guid.Empty;
        _tenantProvider.Setup(p => p.TryGetCurrentTenantId(out noTenant)).Returns(false);

        var tool = CreateTool();
        await tool.InvokeAsync(new AIFunctionArguments { ["query"] = "tax" }, CancellationToken.None);

        _search.Verify(
            s => s.SearchAsync(
                It.IsAny<string>(), It.IsAny<DocumentSearchScope>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never, "no search runs without an authenticated tenant");
    }

    [Fact]
    public async Task SearchMyDocuments_Should_Search_TenantWide_When_User_Has_No_Party()
    {
        _partyResolver
            .Setup(r => r.GetPartyIdForUserAsync(_tenantId, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);
        DocumentSearchScope? captured = null;
        _search
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(), It.IsAny<DocumentSearchScope>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<string, DocumentSearchScope, int, CancellationToken>((_, scope, _, _) => captured = scope)
            .ReturnsAsync(new List<DocumentChunkHit>());

        var tool = CreateTool();
        await tool.InvokeAsync(new AIFunctionArguments { ["query"] = "anything" }, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.OwnerPartyId.Should().BeNull(
            "an unlinked user resolves to no party — the index then restricts to tenant-wide content");
    }

    [Fact]
    public void CreateAll_Should_Yield_Nothing_When_Search_Backend_Missing()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_tenantProvider.Object);
        services.AddSingleton(_currentUserProvider.Object);
        services.AddSingleton(_partyResolver.Object);
        // No IDocumentSearch registered — the tool must self-disable.
        var provider = services.BuildServiceProvider();

        DocumentSearchTools.CreateAll(provider).Should().BeEmpty();
    }
}
