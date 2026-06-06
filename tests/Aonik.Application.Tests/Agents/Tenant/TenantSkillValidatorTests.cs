using Aonik.Agents.Framework;
using FluentAssertions;
using Xunit;

namespace Aonik.Application.Tests.Agents.Tenant;

/// <summary>
/// Spec 033 §8.1 — the allowed-tools ceiling is the key skill safety property. These cover frontmatter
/// parsing + the framework validators, the intersection with the agent's allow-list, and the outright
/// rejection of money-moving built-ins.
/// </summary>
public sealed class TenantSkillValidatorTests
{
    private static readonly string[] AgentTools = { "finance_create_invoice", "finance_graph_get_schema" };

    private readonly ITenantSkillValidator _validator = new TenantSkillValidator();

    private static string Skill(string body) => "---\n" + body + "\n---\n\n# Body\n\nSome procedure.\n";

    [Fact]
    public async Task Validate_Should_Pass_ValidPureInstructionSkill()
    {
        var md = Skill("name: invoice-helper\ndescription: Helps draft invoices for the tenant's billing flow.");

        var result = await _validator.ValidateAsync(md, AgentTools);

        result.IsValid.Should().BeTrue(because: string.Join("; ", result.Errors));
        result.Name.Should().Be("invoice-helper");
        result.AllowedTools.Should().BeEmpty();
    }

    [Fact]
    public async Task Validate_Should_Intersect_AllowedTools_To_AgentToolset()
    {
        var md = Skill(
            "name: invoice-helper\n" +
            "description: Drafts invoices using the available billing tools for this tenant.\n" +
            "allowed-tools: [finance_create_invoice]");

        var result = await _validator.ValidateAsync(md, AgentTools);

        result.IsValid.Should().BeTrue(because: string.Join("; ", result.Errors));
        result.AllowedTools.Should().ContainSingle().Which.Should().Be("finance_create_invoice");
    }

    [Fact]
    public async Task Validate_Should_Reject_Tool_The_Agent_Lacks()
    {
        var md = Skill(
            "name: rogue-skill\n" +
            "description: Tries to reference a tool the agent does not actually have available.\n" +
            "allowed-tools: [some_other_tool]");

        var result = await _validator.ValidateAsync(md, AgentTools);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("some_other_tool"));
    }

    [Fact]
    public async Task Validate_Should_Reject_MoneyMovingBuiltin_Even_When_AgentHasIt()
    {
        var tools = new[] { "finance_capture_payment", "finance_create_invoice" };
        var md = Skill(
            "name: sneaky-skill\n" +
            "description: Attempts to gain access to a money-moving built-in through allowed-tools.\n" +
            "allowed-tools: [finance_capture_payment]");

        var result = await _validator.ValidateAsync(md, tools);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("money-moving"));
    }

    [Fact]
    public async Task Validate_Should_Fail_When_FrontmatterMissing()
    {
        var result = await _validator.ValidateAsync("# Just a heading, no frontmatter", AgentTools);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Validate_Should_Detect_ScriptsPresent()
    {
        var md = Skill(
            "name: scripted-skill\n" +
            "description: A skill that declares helper scripts in its frontmatter block.\n" +
            "scripts:\n  - transform.py");

        var result = await _validator.ValidateAsync(md, AgentTools);

        result.ScriptsPresent.Should().BeTrue();
    }
}
