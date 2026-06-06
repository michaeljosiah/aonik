using Aonik.Agents.Framework;
using FluentAssertions;
using Xunit;

namespace Aonik.Application.Tests.Agents.Tenant;

/// <summary>
/// Spec 033 §8.4 — a tenant HTTP tool's name must be a valid chat-provider function name (else one
/// bad tool breaks every request when the tool list serializes), and the declarative tool may only
/// forward arguments the tenant DECLARED in the parameter schema.
/// </summary>
public sealed class TenantToolNameAndSchemaTests
{
    [Theory]
    [InlineData("finance_post_order", true)]
    [InlineData("tool-1", true)]
    [InlineData("ABC123", true)]
    [InlineData("a", true)]
    [InlineData("my tool", false)]       // space
    [InlineData("my.tool", false)]       // dot
    [InlineData("emoji_😀", false)]      // non-ascii
    [InlineData("has/slash", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ToolName_IsValid_MatchesProviderConstraint(string? name, bool expected)
    {
        ToolNameRules.IsValid(name).Should().Be(expected);
    }

    [Fact]
    public void ToolName_Over64Chars_IsRejected()
    {
        ToolNameRules.IsValid(new string('a', 64)).Should().BeTrue();
        ToolNameRules.IsValid(new string('a', 65)).Should().BeFalse();
    }

    [Fact]
    public void ParseDeclaredParameters_ReturnsPropertyKeys()
    {
        const string schema = """{"type":"object","properties":{"id":{"type":"string"},"limit":{"type":"integer"}}}""";

        DeclarativeHttpAIFunction.ParseDeclaredParameters(schema)
            .Should().BeEquivalentTo("id", "limit");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("{}")]
    [InlineData("{\"type\":\"object\"}")]   // no properties block
    [InlineData("not json")]
    [InlineData("{\"properties\":\"notanobject\"}")]
    public void ParseDeclaredParameters_EmptyWhenNoProperties(string? schema)
    {
        DeclarativeHttpAIFunction.ParseDeclaredParameters(schema).Should().BeEmpty();
    }

    [Fact]
    public void ParseDeclaredParameters_IsCaseSensitive()
    {
        const string schema = """{"properties":{"includeArchived":{"type":"boolean"}}}""";
        var declared = DeclarativeHttpAIFunction.ParseDeclaredParameters(schema);

        declared.Should().Contain("includeArchived");
        // A hallucinated extra (or a case-variant) is not declared and would be filtered out.
        declared.Should().NotContain("includeSecrets");
        declared.Should().NotContain("includearchived");
    }
}
