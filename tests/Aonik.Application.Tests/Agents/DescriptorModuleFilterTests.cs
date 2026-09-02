using Aonik.Agents.Framework;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Modules;
using FluentAssertions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Application.Tests.Agents;

/// <summary>
/// Spec 097 §12.1: a domain agent whose module is disabled for the current tenant is neither
/// listed nor resolvable. Core, unknown and null module ids are never gated.
/// </summary>
public class DescriptorModuleFilterTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public async Task FilterAsync_Should_DropDescriptor_When_ItsModuleIsDisabledForTenant()
    {
        // Arrange
        var commerce = new FakeDescriptor("commerce-agent", ModuleIds.Commerce);
        var core = new FakeDescriptor("orchestrator", ModuleIds.Agents);
        var ungated = new FakeDescriptor("custom-agent", null);
        var unknown = new FakeDescriptor("odd-agent", "not-a-module");
        var filter = CreateFilter(TenantId, new FakeReader(disabled: ModuleIds.Commerce));

        // Act
        var visible = await filter.FilterAsync([commerce, core, ungated, unknown]);

        // Assert
        visible.Should().Equal(core, ungated, unknown);
    }

    [Fact]
    public async Task FilterAsync_Should_KeepDescriptor_When_ItsModuleIsEnabled()
    {
        // Arrange
        var commerce = new FakeDescriptor("commerce-agent", ModuleIds.Commerce);
        var filter = CreateFilter(TenantId, new FakeReader(disabled: ModuleIds.Workspaces));

        // Act
        var visible = await filter.FilterAsync([commerce]);

        // Assert
        visible.Should().ContainSingle().Which.Should().BeSameAs(commerce);
    }

    [Fact]
    public async Task FilterAsync_Should_KeepEverything_When_NoTenantIsResolved()
    {
        // Arrange
        var commerce = new FakeDescriptor("commerce-agent", ModuleIds.Commerce);
        var reader = new FakeReader(disabled: ModuleIds.Commerce);
        var filter = CreateFilter(tenantId: null, reader);

        // Act
        var visible = await filter.FilterAsync([commerce]);

        // Assert
        visible.Should().ContainSingle().Which.Should().BeSameAs(commerce);
        reader.Calls.Should().Be(0, "no tenant means nothing to look up");
    }

    [Fact]
    public async Task FilterAsync_Should_KeepEverything_When_ReaderIsNotRegistered()
    {
        // Arrange
        var commerce = new FakeDescriptor("commerce-agent", ModuleIds.Commerce);
        var filter = CreateFilter(TenantId, reader: null);

        // Act
        var visible = await filter.FilterAsync([commerce]);

        // Assert
        visible.Should().ContainSingle().Which.Should().BeSameAs(commerce);
    }

    [Fact]
    public async Task FilterAsync_Should_NotConsultReader_When_NoDescriptorIsGated()
    {
        // Arrange
        var reader = new FakeReader(disabled: ModuleIds.Commerce);
        var filter = CreateFilter(TenantId, reader);

        // Act
        await filter.FilterAsync([new FakeDescriptor("orchestrator", ModuleIds.Agents), new FakeDescriptor("plain", null)]);

        // Assert
        reader.Calls.Should().Be(0);
    }

    [Fact]
    public async Task FindAsync_Should_ThrowModuleDisabledException_When_ModuleIsDisabled()
    {
        // Arrange
        var commerce = new FakeDescriptor("commerce-agent", ModuleIds.Commerce);
        var filter = CreateFilter(TenantId, new FakeReader(disabled: ModuleIds.Commerce));

        // Act
        var act = () => filter.FindAsync([commerce], "Commerce-Agent");

        // Assert
        (await act.Should().ThrowAsync<ModuleDisabledException>())
            .Which.ModuleId.Should().Be(ModuleIds.Commerce);
    }

    [Fact]
    public async Task FindAsync_Should_ReturnDescriptor_When_ModuleIsEnabled()
    {
        // Arrange
        var commerce = new FakeDescriptor("commerce-agent", ModuleIds.Commerce);
        var filter = CreateFilter(TenantId, new FakeReader());

        // Act
        var found = await filter.FindAsync([commerce], "commerce-agent");

        // Assert
        found.Should().BeSameAs(commerce);
    }

    [Fact]
    public async Task FindAsync_Should_ReturnNull_When_NoDescriptorHasThatName()
    {
        // Arrange
        var filter = CreateFilter(TenantId, new FakeReader());

        // Act
        var found = await filter.FindAsync([new FakeDescriptor("orchestrator", ModuleIds.Agents)], "missing");

        // Assert
        found.Should().BeNull();
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("not-a-module", false)]
    [InlineData(ModuleIds.Platform, false)]
    [InlineData(ModuleIds.Agents, false)]
    [InlineData(ModuleIds.Ai, false)]
    [InlineData(ModuleIds.Ordering, false)]
    [InlineData(ModuleIds.Commerce, true)]
    [InlineData(ModuleIds.PersonalFinance, true)]
    public void IsGated_Should_BeTrue_When_ModuleIsKnownAndNotCore(string? moduleId, bool expected)
    {
        DescriptorModuleFilter.IsGated(moduleId).Should().Be(expected);
    }

    [Fact]
    public void DefaultModuleId_Should_BeNull_When_DescriptorAssemblyCarriesNoModuleAttribute()
    {
        // Arrange: the test assembly has no AonikModuleAttribute, so the interface default is null.
        IDomainAgentDescriptor descriptor = new DefaultModuleIdDescriptor();

        // Assert
        descriptor.ModuleId.Should().BeNull();
    }

    private static DescriptorModuleFilter CreateFilter(Guid? tenantId, IModuleEnablementReader? reader)
        => new(new OptionalTenantProvider(tenantId), NullLogger<DescriptorModuleFilter>.Instance, reader);

    private sealed class OptionalTenantProvider(Guid? tenantId) : ITenantProvider
    {
        public Guid GetCurrentTenantId() => tenantId ?? throw new InvalidOperationException("No tenant.");

        public bool TryGetCurrentTenantId(out Guid id)
        {
            id = tenantId ?? Guid.Empty;
            return tenantId.HasValue;
        }
    }

    private sealed class FakeReader(params string[] disabled) : IModuleEnablementReader
    {
        public int Calls { get; private set; }

        public Task<ModuleEnablementSet> GetAsync(Guid tenantId, CancellationToken ct = default)
        {
            Calls++;
            var enabled = ModuleCatalog.All.Select(m => m.Id).Except(disabled, StringComparer.Ordinal)
                .ToHashSet(StringComparer.Ordinal);
            return Task.FromResult(new ModuleEnablementSet(tenantId, enabled));
        }

        public Task<IReadOnlyList<Guid>> FilterEnabledTenantsAsync(
            IEnumerable<Guid> tenantIds, string moduleId, CancellationToken ct = default)
        {
            Calls++;
            IReadOnlyList<Guid> result = disabled.Contains(moduleId, StringComparer.Ordinal)
                ? []
                : tenantIds.Distinct().ToList();
            return Task.FromResult(result);
        }
    }

    private sealed class DefaultModuleIdDescriptor : IDomainAgentDescriptor
    {
        public string Name => "default-module-agent";

        public string Description => "Uses the interface default for ModuleId.";

        public AIAgent Build(IChatClient chatClient, IServiceProvider serviceProvider)
            => throw new NotSupportedException("Not built in this test.");

        public IReadOnlyList<string> GetToolNames(IServiceProvider serviceProvider) => [];
    }

    private sealed class FakeDescriptor(string name, string? moduleId) : IDomainAgentDescriptor
    {
        public string Name => name;

        public string Description => $"Fake agent '{name}'.";

        public string? ModuleId => moduleId;

        public AIAgent Build(IChatClient chatClient, IServiceProvider serviceProvider)
            => throw new NotSupportedException("Not built in this test.");

        public IReadOnlyList<string> GetToolNames(IServiceProvider serviceProvider) => [];
    }
}
