using Aonik.Cli;
using Aonik.Cli.Commands;
using Aonik.Cli.Infrastructure;
using Aonik.Cli.Models;
using Aonik.Cli.Tests.Support;

using FluentAssertions;

namespace Aonik.Cli.Tests;

/// <summary>
/// Selection shaping for the Spec 066 storefront commands. The API is strict about wire shape —
/// a multi-select group takes an array even for one choice — and the CLI's job is to translate
/// <c>--select group=choice</c> into that shape so the user never has to know it exists.
/// </summary>
public class CommerceCommandHandlerTests
{
    private static readonly CliEffectiveOptionGroup Protein = new(
        "protein", "Protein", null, "Multi", "GBP", 1, "salmon",
        [
            new CliEffectiveOptionChoice("salmon", "Salmon", null, 3m, 0),
            new CliEffectiveOptionChoice("prawns", "King prawns", null, 3m, 1),
        ]);

    private static readonly CliEffectiveOptionGroup Portion = new(
        "portion", "Portion", null, "One", "GBP", 2, "light",
        [
            new CliEffectiveOptionChoice("light", "Light table", null, 0m, 0),
            new CliEffectiveOptionChoice("full", "Full table", null, 10m, 1),
        ]);

    // ─── ParseSelections ─────────────────────────────────────────────────────

    [Fact]
    public void ParseSelections_Should_KeepRawArrays_WithoutDecidingShape()
    {
        // The parser cannot know a group's selection mode, so it must not collapse a single value
        // to a scalar — that decision belongs to shaping, where the mode is known. Collapsing here
        // is exactly how the CLI once became unable to express a one-choice multi-selection.
        var parsed = CommerceCommandHandler.ParseSelections(["protein=salmon", "extras=cheese,olives"]);

        parsed["protein"].Should().Equal("salmon");
        parsed["extras"].Should().Equal("cheese", "olives");
    }

    // ─── ShapeSelections ─────────────────────────────────────────────────────

    [Fact]
    public void ShapeSelections_Should_WrapSingleValue_ForMultiSelectGroup()
    {
        var shaped = CommerceCommandHandler.ShapeSelections(
            new Dictionary<string, string[]> { ["protein"] = ["salmon"] }, [Protein, Portion]);

        shaped["protein"].Should().BeOfType<string[]>().Which.Should().Equal("salmon");
    }

    [Fact]
    public void ShapeSelections_Should_UnwrapSingleValue_ForSingleSelectGroup()
    {
        var shaped = CommerceCommandHandler.ShapeSelections(
            new Dictionary<string, string[]> { ["portion"] = ["full"] }, [Protein, Portion]);

        shaped["portion"].Should().BeOfType<string>().Which.Should().Be("full");
    }

    [Fact]
    public void ShapeSelections_Should_LeaveUnknownGroupsAlone_SoTheServerNamesTheProblem()
    {
        // A group the product does not offer has no mode to shape by. Pass it through and let the
        // server answer V1 ("does not offer this group") — a CLI guess would mask the real error.
        var shaped = CommerceCommandHandler.ShapeSelections(
            new Dictionary<string, string[]> { ["made-up"] = ["x"] }, [Protein, Portion]);

        shaped["made-up"].Should().BeOfType<string>().Which.Should().Be("x");
    }

    // ─── QuoteAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task QuoteAsync_Should_SendAnArray_ForASingleChoiceOnAMultiSelectGroup()
    {
        // The regression that motivated this file: `--select protein=salmon` on a Multi group was
        // sent as a bare string, which the strict endpoint rejects with V5 — making a perfectly
        // valid one-choice multi-selection inexpressible from the CLI.
        var (handler, apiClient, _) = CreateHandler();
        apiClient.StorefrontProduct = Product(Protein, Portion);

        await handler.QuoteAsync(Target(), "jollof", ["protein=salmon"], "GBP", OutputMode.Json);

        apiClient.LastSelection.Should().NotBeNull();
        apiClient.LastSelection!["protein"].Should().BeOfType<string[]>().Which.Should().Equal("salmon");
    }

    [Fact]
    public async Task QuoteAsync_Should_SendAScalar_ForASingleSelectGroup()
    {
        var (handler, apiClient, _) = CreateHandler();
        apiClient.StorefrontProduct = Product(Protein, Portion);

        await handler.QuoteAsync(Target(), "jollof", ["portion=full"], "GBP", OutputMode.Json);

        apiClient.LastSelection!["portion"].Should().BeOfType<string>().Which.Should().Be("full");
    }

    // ─── VerifyAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task VerifyAsync_Should_ShapeEverySubmittedSelection_ByItsGroupsMode()
    {
        // The gate must not fail a CORRECT implementation. With a multi-select group present, every
        // probe that touches it — canonical-stability, difference-pricing, the unknown-choice
        // rejection — must submit an array, or the API rightly answers V5 and the deployment gate
        // reports a healthy deployment as broken.
        //
        // Deliberately NOT asserted: the exit code. The fake returns one fixed quote for every
        // call, so the checks' own assertions cannot all pass here — this test pins the shapes.
        var (handler, apiClient, _) = CreateHandler();
        apiClient.StorefrontProduct = Product(Protein, Portion);

        await handler.VerifyAsync(Target(), "jollof", "GBP", OutputMode.Json);

        var touchingProtein = apiClient.QuoteSelections.Where(s => s.ContainsKey("protein")).ToList();
        touchingProtein.Should().NotBeEmpty("verify must exercise the multi-select group");
        touchingProtein.Should().OnlyContain(
            s => s["protein"] is string[],
            "a multi-select group must always be submitted as an array");

        apiClient.QuoteSelections
            .Where(s => s.ContainsKey("portion"))
            .Should().OnlyContain(
                s => s["portion"] is string,
                "a single-select group must always be submitted as a bare string");
    }

    // ─── Plumbing ────────────────────────────────────────────────────────────

    private static (CommerceCommandHandler Handler, FakeAonikCliApiClient ApiClient, StringWriter Writer) CreateHandler()
    {
        var apiClient = new FakeAonikCliApiClient();
        var writer = new StringWriter();
        return (new CommerceCommandHandler(apiClient, new TextWriterCliOutputWriter(writer)), apiClient, writer);
    }

    private static StorefrontTarget Target() => new("https://api.aonik.local", Guid.NewGuid());

    private static CliStorefrontProduct Product(params CliEffectiveOptionGroup[] groups)
        => new(Guid.NewGuid(), "jollof", "Jollof Rice", "Active", null, null, groups);
}
