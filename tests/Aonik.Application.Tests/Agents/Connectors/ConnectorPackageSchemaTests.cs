using System.Text.Json;

using FluentAssertions;

using Aonik.Agents.Services.Connectors.Catalog;

namespace Aonik.Application.Tests.Agents.Connectors;

public sealed class ConnectorPackageSchemaTests
{
    [Theory]
    [InlineData("{\"id\":\"invoice-1\",\"currency\":\"GBP\",\"amount\":10}", true)]
    [InlineData("{\"id\":\"invoice-1\",\"currency\":\"gbp\",\"amount\":10}", false)]
    [InlineData("{\"id\":\"invoice-1\",\"currency\":\"GBP\",\"amount\":-1}", false)]
    [InlineData("{\"id\":\"invoice-1\",\"currency\":\"GBP\",\"amount\":10,\"unsafe\":true}", false)]
    [InlineData("{\"id\":\"invoice-1\",\"currency\":\"GBP\",\"amount\":10,\"amount\":20}", false)]
    [InlineData("{\"Id\":\"invoice-1\",\"currency\":\"GBP\",\"amount\":10}", false)]
    public void Arguments_Should_MatchTheReviewedSchema_WithoutReinterpretingLiteralKeysOrValues(string arguments, bool valid)
    {
        using JsonDocument declaration = JsonDocument.Parse("""{"type":"object","properties":{"id":{"type":"string","minLength":1},"currency":{"enum":["GBP"]},"amount":{"type":"number","minimum":0}},"required":["id","currency","amount"],"additionalProperties":false}""");
        var schema = new ConnectorPackageSchema(declaration.RootElement);
        using JsonDocument input = JsonDocument.Parse(arguments);

        Action validate = () => schema.Require(input.RootElement, 4096);

        if (valid) validate.Should().NotThrow(); else validate.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("{\"type\":\"object\",\"$ref\":\"https://private.example.test/schema\"}")]
    [InlineData("{\"type\":\"object\",\"$ref\":\"#\"}")]
    [InlineData("{\"type\":\"object\",\"properties\":{\"id\":{\"type\":\"string\",\"pattern\":\"(a+)+$\"}}}")]
    [InlineData("{\"type\":\"object\",\"$schema\":\"https://private.example.test/meta\"}")]
    public void Declaration_Should_RefuseExternalResolutionCyclesAndUnboundedRegularExpressions(string declaration)
    {
        using JsonDocument input = JsonDocument.Parse(declaration);

        Action compile = () => new ConnectorPackageSchema(input.RootElement);

        compile.Should().Throw<InvalidOperationException>().WithMessage("ConnectorPackage.ActionSchemaUnsupported");
    }

    [Fact]
    public void Declaration_Should_PreserveLiteralDataNamedLikeSchemaKeywords_AndBoundUtf8Bytes()
    {
        using JsonDocument declaration = JsonDocument.Parse("""{"type":"object","properties":{"$ref":{"type":"string"},"pattern":{"const":"éé"}},"required":["$ref","pattern"],"additionalProperties":false}""");
        using JsonDocument input = JsonDocument.Parse("""{"$ref":"literal","pattern":"éé"}""");
        var schema = new ConnectorPackageSchema(declaration.RootElement);

        Action valid = () => schema.Require(input.RootElement, 100);
        Action oversized = () => schema.Require(input.RootElement, 10);

        valid.Should().NotThrow(); oversized.Should().Throw<ArgumentException>();
    }
}
