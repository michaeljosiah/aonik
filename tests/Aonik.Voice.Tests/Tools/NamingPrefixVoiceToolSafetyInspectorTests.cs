using Aonik.Voice.Tools;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace Aonik.Voice.Tests.Tools;

public class NamingPrefixVoiceToolSafetyInspectorTests
{
    private readonly NamingPrefixVoiceToolSafetyInspector _inspector = new();

    // ── Classify ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("pf_get_accounts")]
    [InlineData("pf_list_invoices")]
    [InlineData("pf_search_payees")]
    [InlineData("pf_describe_account")]
    [InlineData("pf_summarize_spending")]
    [InlineData("PF_GET_TOTALS")]   // case-insensitive
    public void Classify_Should_Return_ReadOnly_For_Known_ReadOnly_Prefix(string toolName)
    {
        _inspector.Classify(toolName).Should().Be(VoiceToolClassification.ReadOnly);
    }

    [Theory]
    [InlineData("pf_create_invoice")]
    [InlineData("pf_update_account")]
    [InlineData("pf_archive_proposal")]
    [InlineData("pf_delete_rule")]
    [InlineData("pf_apply_proposal")]
    [InlineData("pf_cancel_payment")]
    [InlineData("pf_confirm_action")]
    [InlineData("pf_reject_proposal")]
    [InlineData("pf_set_preference")]
    [InlineData("pf_override_category")]
    [InlineData("PF_CREATE_BILL")]   // case-insensitive
    public void Classify_Should_Return_Mutating_For_Known_Mutating_Prefix(string toolName)
    {
        _inspector.Classify(toolName).Should().Be(VoiceToolClassification.Mutating);
    }

    [Theory]
    [InlineData("user_memory_recall")]
    [InlineData("display_chart")]
    [InlineData("confirmAction")]
    [InlineData("totally_made_up_tool")]
    public void Classify_Should_Return_Unknown_For_Unmatched_Names(string toolName)
    {
        // Unknowns are treated as mutating to fail safe — caller's job to surface
        // the warning to the maintainer so the prefix lists get updated.
        _inspector.Classify(toolName).Should().Be(VoiceToolClassification.Unknown);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Classify_Should_Return_Unknown_For_NullOrWhitespace(string? toolName)
    {
        _inspector.Classify(toolName!).Should().Be(VoiceToolClassification.Unknown);
    }

    // ── FilterReadOnlyNames ────────────────────────────────────────────────

    [Fact]
    public void FilterReadOnlyNames_Should_Return_Only_ReadOnly_Names()
    {
        var input = new[]
        {
            "pf_get_accounts",        // read-only
            "pf_list_invoices",       // read-only
            "pf_create_invoice",      // mutating
            "pf_update_account",      // mutating
            "user_memory_recall",     // unknown — excluded to fail safe
        };

        var result = _inspector.FilterReadOnlyNames(input);

        result.Should().BeEquivalentTo("pf_get_accounts", "pf_list_invoices");
        result.Should().NotContain("pf_create_invoice");
        result.Should().NotContain("pf_update_account");
        result.Should().NotContain("user_memory_recall");
    }

    [Fact]
    public void FilterReadOnlyNames_Should_Be_Case_Insensitive()
    {
        var input = new[] { "PF_GET_ACCOUNTS", "pf_create_invoice" };

        var result = _inspector.FilterReadOnlyNames(input);

        result.Should().HaveCount(1);
        result.Contains("pf_get_accounts").Should().BeTrue("HashSet should be case-insensitive");
    }

    [Fact]
    public void FilterReadOnlyNames_Should_Return_Empty_For_Null_Or_Empty_Input()
    {
        _inspector.FilterReadOnlyNames(Array.Empty<string>()).Should().BeEmpty();
        _inspector.FilterReadOnlyNames(null!).Should().BeEmpty();
    }

    // ── FilterReadOnly (AITool) ────────────────────────────────────────────

    [Fact]
    public void FilterReadOnly_Should_Drop_Mutating_And_Unknown_AITool_Instances()
    {
        var tools = new AITool[]
        {
            AIFunctionFactory.Create(() => "ok", "pf_get_accounts"),
            AIFunctionFactory.Create(() => "ok", "pf_create_invoice"),
            AIFunctionFactory.Create(() => "ok", "user_memory_recall"),
        };

        var filtered = _inspector.FilterReadOnly(tools);

        filtered.Should().HaveCount(1);
        filtered[0].Name.Should().Be("pf_get_accounts");
    }

    // ── Custom prefix configuration ────────────────────────────────────────

    [Fact]
    public void Custom_Constructor_Should_Use_Supplied_Prefix_Lists()
    {
        // Tenants or future configuration may want to widen/narrow the prefix
        // sets (e.g. add `pl_get_` for a Platform agent's tools).
        var custom = new NamingPrefixVoiceToolSafetyInspector(
            readOnlyPrefixes: new[] { "ro_" },
            mutatingPrefixes: new[] { "mut_" });

        custom.Classify("ro_anything").Should().Be(VoiceToolClassification.ReadOnly);
        custom.Classify("mut_anything").Should().Be(VoiceToolClassification.Mutating);
        custom.Classify("pf_get_accounts").Should().Be(VoiceToolClassification.Unknown,
            "the default prefixes don't apply when the caller supplies their own lists");
    }
}
