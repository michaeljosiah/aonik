using Aonik.Agents.Framework;
using FluentAssertions;
using Xunit;

namespace Aonik.Application.Tests.Agents.Tenant;

/// <summary>
/// Spec 033 §8.2 — a script-bearing skill's scripts are injectable only when a PlatformAdmin has
/// enabled them AND the deployment allows skill scripts. Otherwise the skill is materialised with
/// scripts stripped (instructions/references still work), so the "enable scripts" review control is
/// meaningful for script-bearing skills.
/// </summary>
public sealed class TenantSkillScriptGateTests
{
    [Theory]
    // scriptsPresent, scriptsEnabled, allowSkillScripts -> injectable?
    [InlineData(false, false, false, false)] // no scripts at all
    [InlineData(false, true, true, false)]   // no scripts: nothing to inject regardless of flags
    [InlineData(true, false, false, false)]  // present, not enabled, deployment off -> stripped
    [InlineData(true, true, false, false)]   // present, enabled, but deployment disallows -> stripped
    [InlineData(true, false, true, false)]   // present, deployment allows, but not enabled -> stripped
    [InlineData(true, true, true, true)]     // present + enabled + allowed -> injectable
    public void ScriptsInjectable_RequiresBothGates(bool present, bool enabled, bool allow, bool expected)
    {
        TenantSkillsProviderFactory.ScriptsInjectable(present, enabled, allow).Should().Be(expected);
    }
}
