using Aonik.SharedKernel.Modules;
using Aonik.Worker.Jobs;

using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using Moq;

namespace Aonik.Application.Tests.Worker;

/// <summary>
/// Spec 097 §12.2 / acceptance 11: the shared helper every gated Worker job funnels its tenant
/// fan-out through. It must be a no-op for a null, core or unknown module id and for a host with
/// no reader, and otherwise drop the disabled tenants, count them, and phrase the skip note the
/// jobs append to their execution result.
/// </summary>
public class ModuleGatedTenantsTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();
    private static readonly Guid TenantC = Guid.NewGuid();

    [Theory]
    [InlineData(null)]
    [InlineData(ModuleIds.Ai)]
    [InlineData(ModuleIds.Platform)]
    [InlineData("not-a-module")]
    public async Task FilterAsync_Should_ReturnInputUnchanged_When_ModuleIsNullCoreOrUnknown(string? moduleId)
    {
        // Arrange
        var reader = new Mock<IModuleEnablementReader>(MockBehavior.Strict);
        var input = new[] { TenantA, TenantB };

        // Act
        var result = await ModuleGatedTenants.FilterAsync(
            reader.Object, input, moduleId, "job", NullLogger.Instance, CancellationToken.None);

        // Assert
        result.Enabled.Should().BeEquivalentTo(input);
        result.Skipped.Should().Be(0);
        result.Note.Should().BeEmpty();
        reader.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task FilterAsync_Should_ReturnInputUnchanged_When_ReaderIsNull()
    {
        // Arrange
        var input = new[] { TenantA, TenantB };

        // Act
        var result = await ModuleGatedTenants.FilterAsync(
            reader: null, input, ModuleIds.Workspaces, "job", NullLogger.Instance, CancellationToken.None);

        // Assert
        result.Enabled.Should().BeEquivalentTo(input, "a host without the module graph gates nothing");
        result.Skipped.Should().Be(0);
        result.Note.Should().BeEmpty();
    }

    [Fact]
    public async Task FilterAsync_Should_ReturnInputUnchanged_When_TenantListIsEmpty()
    {
        // Arrange
        var reader = new Mock<IModuleEnablementReader>(MockBehavior.Strict);

        // Act
        var result = await ModuleGatedTenants.FilterAsync(
            reader.Object, [], ModuleIds.Workspaces, "job", NullLogger.Instance, CancellationToken.None);

        // Assert
        result.Enabled.Should().BeEmpty();
        result.Skipped.Should().Be(0);
        reader.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task FilterAsync_Should_DropDisabledTenantsAndCountThem_When_ModuleIsNonCore()
    {
        // Arrange — the reader keeps A and C; B has workspaces off. A duplicate B must not count twice.
        var reader = new Mock<IModuleEnablementReader>();
        reader
            .Setup(r => r.FilterEnabledTenantsAsync(It.IsAny<IEnumerable<Guid>>(), ModuleIds.Workspaces, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid> { TenantA, TenantC });

        // Act
        var result = await ModuleGatedTenants.FilterAsync(
            reader.Object, [TenantA, TenantB, TenantB, TenantC], ModuleIds.Workspaces, "Workspace blob sweep",
            NullLogger.Instance, CancellationToken.None);

        // Assert
        result.Enabled.Should().BeEquivalentTo(new[] { TenantA, TenantC });
        result.Skipped.Should().Be(1, "distinct input minus enabled");
        result.ModuleId.Should().Be(ModuleIds.Workspaces);
        result.Note.Should().Be($" Skipped 1 tenant(s) with module '{ModuleIds.Workspaces}' disabled.");
        reader.Verify(
            r => r.FilterEnabledTenantsAsync(It.IsAny<IEnumerable<Guid>>(), ModuleIds.Workspaces, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void IsGated_Should_BeTrueOnlyForKnownNonCoreModules()
    {
        ModuleGatedTenants.IsGated(ModuleIds.Commerce).Should().BeTrue();
        ModuleGatedTenants.IsGated(ModuleIds.Agents).Should().BeFalse("core");
        ModuleGatedTenants.IsGated("not-a-module").Should().BeFalse("unknown");
        ModuleGatedTenants.IsGated(null).Should().BeFalse();
    }
}
