using FluentAssertions;

using Aonik.Ai.Services;

namespace Aonik.Api.Tests;

public class SpeechTextNormalizerTests
{
    // ── Currency: symbol + amount + code (the doubling problem) ──────────

    [Theory]
    [InlineData("£380 GBP", "380 pounds")]
    [InlineData("€1,890 EUR", "1,890 euros")]
    [InlineData("$50 USD", "50 dollars")]
    [InlineData("£1 GBP", "1 pound")]
    [InlineData("€1 EUR", "1 euro")]
    public void Normalize_Should_DeduplicateSymbolAndCode(string input, string expected)
    {
        SpeechTextNormalizer.Normalize(input).Should().Be(expected);
    }

    // ── Currency: amount + code only (no symbol) ────────────────────────

    [Theory]
    [InlineData("45,000 XOF", "45,000 CFA francs")]
    [InlineData("75,000 XOF", "75,000 CFA francs")]
    [InlineData("1,500 GBP", "1,500 pounds")]
    [InlineData("900 EUR", "900 euros")]
    [InlineData("1 USD", "1 dollar")]
    public void Normalize_Should_ReplaceCodeWithSpokenName(string input, string expected)
    {
        SpeechTextNormalizer.Normalize(input).Should().Be(expected);
    }

    // ── Currency: symbol + amount only (no code) → providers handle ─────

    [Theory]
    [InlineData("£280", "£280")]
    [InlineData("€150", "€150")]
    public void Normalize_Should_LeaveSymbolOnlyAmountsAlone(string input, string expected)
    {
        SpeechTextNormalizer.Normalize(input).Should().Be(expected);
    }

    // ── Approximate amounts ─────────────────────────────────────────────

    [Fact]
    public void Normalize_Should_ConvertApproximateAmounts()
    {
        SpeechTextNormalizer.Normalize("~£450 GBP equivalent")
            .Should().Be("approximately 450 pounds equivalent");
    }

    [Fact]
    public void Normalize_Should_ConvertApproximateAmountsWithoutSymbol()
    {
        SpeechTextNormalizer.Normalize("~450 GBP")
            .Should().Be("approximately 450 pounds");
    }

    // ── UUIDs ────────────────────────────────────────────────────────────

    [Fact]
    public void Normalize_Should_RemoveParenthesizedUuids()
    {
        var input = "Senelec electricity (bill b4444444-4444-4444-4444-444444444444)";
        SpeechTextNormalizer.Normalize(input)
            .Should().Be("Senelec electricity");
    }

    [Fact]
    public void Normalize_Should_RemoveBareUuids()
    {
        var input = "bill b4444444-4444-4444-4444-444444444444 is due";
        SpeechTextNormalizer.Normalize(input)
            .Should().Be("bill is due");
    }

    // ── Markdown ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("**bold text**", "bold text")]
    [InlineData("*italic text*", "italic text")]
    [InlineData("***bold italic***", "bold italic")]
    [InlineData("__underline__", "underline")]
    public void Normalize_Should_StripMarkdownEmphasis(string input, string expected)
    {
        SpeechTextNormalizer.Normalize(input).Should().Be(expected);
    }

    [Fact]
    public void Normalize_Should_StripBulletPoints()
    {
        var input = "- First item\n- Second item\n* Third item";
        var result = SpeechTextNormalizer.Normalize(input);
        result.Should().Contain("First item");
        result.Should().Contain("Second item");
        result.Should().Contain("Third item");
        result.Should().NotContain("- ");
        result.Should().NotContain("* ");
    }

    // ── Full integration (the actual failing example) ───────────────────

    [Fact]
    public void Normalize_Should_FixTheRealWorldPayaboExample()
    {
        var input = """
            Nice — quick money snapshot for you, Amara.

            - You've got £380 GBP total in cash, with £280 GBP available.
            - Upcoming bills: Senelec — electricity 45,000 XOF due 2026-04-07 (bill b4444444-4444-4444-4444-444444444444) and Papa medical appointment 75,000 XOF due 2026-04-15 (bill b5555555-5555-5555-5555-555555555555).
            - Emergency fund: £120 of £1,500 target (behind schedule).
            - Spending (2026-03-01 to 2026-03-31): €1,890 EUR total — biggest pressure is Family Support (€1,240 EUR vs budget €900 EUR) and Groceries (€180 EUR vs budget €150 EUR).
            - Risk note: your available £280 GBP is below estimated upcoming obligations (~£450 GBP equivalent), so a shortfall is likely this month.

            Want me to prioritise which bill to cover, check cheapest XOF transfer options, or build a quick plan to cover the shortfall?
            """;

        var result = SpeechTextNormalizer.Normalize(input);

        // Currency doubling should be gone
        result.Should().NotContain("pounds pounds");
        result.Should().NotContain("euros euros");
        result.Should().NotContain("dollars dollars");

        // XOF should be humanised
        result.Should().Contain("45,000 CFA francs");
        result.Should().Contain("75,000 CFA francs");

        // UUIDs should be gone
        result.Should().NotContain("4444444-4444");
        result.Should().NotContain("5555555-5555");
        result.Should().NotContain("(bill");

        // Markdown bullets should be gone
        result.Should().NotContain("\n- ");

        // Approximate amount should be spoken naturally
        result.Should().Contain("approximately 450 pounds");

        // Core content should remain
        result.Should().Contain("380 pounds");
        result.Should().Contain("280 pounds");
        result.Should().Contain("1,890 euros");
        result.Should().Contain("Senelec");
        result.Should().Contain("Amara");
    }

    // ── Edge cases ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void Normalize_Should_HandleNullAndEmpty(string? input, string expected)
    {
        SpeechTextNormalizer.Normalize(input).Should().Be(expected);
    }

    [Fact]
    public void Normalize_Should_NotTouchUnknownCurrencyCodes()
    {
        SpeechTextNormalizer.Normalize("100 XYZ").Should().Be("100 XYZ");
    }

    [Fact]
    public void Normalize_Should_PreservePlainText()
    {
        var input = "Hello, this is a simple sentence with no special formatting.";
        SpeechTextNormalizer.Normalize(input).Should().Be(input);
    }

    [Fact]
    public void Normalize_Should_HandleMultipleCurrenciesInOneSentence()
    {
        var input = "Convert £100 GBP to 45,000 XOF or €85 EUR";
        var result = SpeechTextNormalizer.Normalize(input);
        result.Should().Be("Convert 100 pounds to 45,000 CFA francs or 85 euros");
    }
}
