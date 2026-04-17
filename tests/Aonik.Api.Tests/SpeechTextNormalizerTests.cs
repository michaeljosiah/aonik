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
        // An unknown 3-letter code preceded by an amount is left intact — we
        // neither humanise it (we don't know the currency name) nor hyphenate
        // it via the acronym rule (the lookbehind guards "\d\s?").
        SpeechTextNormalizer.Normalize("100 XYZ").Should().Be("100 XYZ");
    }

    // ── Currency: code + amount (e.g. "GBP 200", "USD 1,250.50") ────────

    [Theory]
    [InlineData("GBP 200", "200 pounds")]
    [InlineData("USD 1,500", "1,500 dollars")]
    [InlineData("EUR 1,250.50", "1,250.50 euros")]
    [InlineData("NGN 45000", "45000 naira")]
    [InlineData("XOF 45,000", "45,000 CFA francs")]
    [InlineData("KES 1,000,000", "1,000,000 Kenyan shillings")]
    [InlineData("JPY 9000", "9000 yen")]
    public void Normalize_Should_ReplaceCodeBeforeAmount(string input, string expected)
    {
        SpeechTextNormalizer.Normalize(input).Should().Be(expected);
    }

    // ── Currency: singular handling for "1 unit" (both orderings) ───────

    [Theory]
    [InlineData("1 GBP", "1 pound")]
    [InlineData("GBP 1", "1 pound")]
    [InlineData("£1 GBP", "1 pound")]
    [InlineData("1 USD", "1 dollar")]
    [InlineData("USD 1", "1 dollar")]
    [InlineData("EUR 1", "1 euro")]
    public void Normalize_Should_HandleSingularAmounts(string input, string expected)
    {
        SpeechTextNormalizer.Normalize(input).Should().Be(expected);
    }

    // ── Currency: negative amounts preserve the sign ────────────────────

    [Theory]
    [InlineData("-500 GBP", "-500 pounds")]
    [InlineData("USD -500", "-500 dollars")]
    [InlineData("USD -1,250.50", "-1,250.50 dollars")]
    public void Normalize_Should_PreserveNegativeAmounts(string input, string expected)
    {
        SpeechTextNormalizer.Normalize(input).Should().Be(expected);
    }

    // ── Currency: decimal amounts (both orderings) ──────────────────────

    [Theory]
    [InlineData("200.50 GBP", "200.50 pounds")]
    [InlineData("GBP 200.50", "200.50 pounds")]
    [InlineData("USD 1,250.99", "1,250.99 dollars")]
    [InlineData("£12.50 GBP", "12.50 pounds")]
    public void Normalize_Should_HandleDecimalAmounts(string input, string expected)
    {
        SpeechTextNormalizer.Normalize(input).Should().Be(expected);
    }

    // ── Currency: additional symbols beyond £/€/$ ───────────────────────

    [Theory]
    [InlineData("¥5000 JPY", "5000 yen")]
    [InlineData("₦1,000 NGN", "1,000 naira")]
    [InlineData("₹500 INR", "500 rupees")]
    [InlineData("₵50 GHS", "50 cedis")]
    public void Normalize_Should_HandleNonLatinCurrencySymbols(string input, string expected)
    {
        SpeechTextNormalizer.Normalize(input).Should().Be(expected);
    }

    // ── Currency: mid-sentence placement + sentence punctuation ─────────

    [Theory]
    [InlineData("I paid GBP 200 yesterday.", "I paid 200 pounds yesterday.")]
    [InlineData("The bill is 200 GBP.", "The bill is 200 pounds.")]
    [InlineData("Total: £200 GBP!", "Total: 200 pounds!")]
    [InlineData("Is it USD 500?", "Is it 500 dollars?")]
    public void Normalize_Should_HandleCurrencyMidSentence(string input, string expected)
    {
        SpeechTextNormalizer.Normalize(input).Should().Be(expected);
    }

    // ── Currency: mixed orderings in a single string ────────────────────

    [Fact]
    public void Normalize_Should_HandleCodeFirstAndCodeLastInSameSentence()
    {
        // Independent pairs (each pair is unambiguous on its own) — one uses
        // amount-before-code, the other code-before-amount.
        var result = SpeechTextNormalizer.Normalize("I paid 100 GBP and received EUR 200 back");
        result.Should().Be("I paid 100 pounds and received 200 euros back");
    }

    [Fact]
    public void Normalize_Should_HandleMultipleCodeBeforeAmountPairs()
    {
        var result = SpeechTextNormalizer.Normalize("Breakdown: GBP 100, USD 200, EUR 300.");
        result.Should().Be("Breakdown: 100 pounds, 200 dollars, 300 euros.");
    }

    // ── Currency: unknown code in code-first position is left alone ─────

    [Fact]
    public void Normalize_Should_NotTouchUnknownCodeBeforeAmount()
    {
        // "XYZ 100" is an unknown currency code — we don't humanise it, but
        // the acronym rule still spells it out because nothing preceding it
        // looks like a digit-amount context. That's deliberate: unrecognised
        // three-letter tokens read as acronyms.
        SpeechTextNormalizer.Normalize("XYZ 100").Should().Be("X-Y-Z 100");
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

    // ── Emoji removal ───────────────────────────────────────────────────

    [Theory]
    [InlineData("Hello 👋 world", "Hello world")]
    [InlineData("Pay complete ✅", "Pay complete")]
    [InlineData("Flag 🇬🇧 currency", "Flag currency")]
    [InlineData("Mixed 🎉 content 💰 here", "Mixed content here")]
    public void Normalize_Should_StripEmojis(string input, string expected)
    {
        SpeechTextNormalizer.Normalize(input).Should().Be(expected);
    }

    // ── Percent expansion ───────────────────────────────────────────────

    [Theory]
    [InlineData("APR is 45%", "A-P-R is 45 percent")]
    [InlineData("up 12% this month", "up 12 percent this month")]
    [InlineData("100% complete", "100 percent complete")]
    public void Normalize_Should_ExpandPercent(string input, string expected)
    {
        SpeechTextNormalizer.Normalize(input).Should().Be(expected);
    }

    // ── Directional / math / trademark symbols ──────────────────────────

    [Theory]
    [InlineData("budget → spending", "budget to spending")]
    [InlineData("rate ↑ slightly", "rate up slightly")]
    [InlineData("Monday – Friday", "Monday to Friday")]
    [InlineData("Senelec — electricity", "Senelec, electricity")]
    [InlineData("a > b", "a greater than b")]
    [InlineData("x <= y", "x less than or equal to y")]
    [InlineData("foo -> bar", "foo to bar")]
    [InlineData("Aonik™ product", "Aonik trademark product")]
    public void Normalize_Should_ReplaceSymbolsWithSpokenWords(string input, string expected)
    {
        SpeechTextNormalizer.Normalize(input).Should().Be(expected);
    }

    // ── Acronym expansion ───────────────────────────────────────────────

    [Theory]
    [InlineData("The FBI investigation", "The F-B-I investigation")]
    [InlineData("Contact the CIA or NSA", "Contact the C-I-A or N-S-A")]
    [InlineData("ASAP please", "A-S-A-P please")]
    [InlineData("NASA launched the rocket", "N-A-S-A launched the rocket")]
    public void Normalize_Should_SpellOutAcronyms(string input, string expected)
    {
        SpeechTextNormalizer.Normalize(input).Should().Be(expected);
    }

    [Fact]
    public void Normalize_Should_NotTouchCurrencyCodesAsAcronyms()
    {
        // Currency codes are processed by the currency pipeline and must not
        // also be hyphenated by the acronym step. "USD" is always "dollars"
        // after normalization, never "U-S-D".
        var result = SpeechTextNormalizer.Normalize("The transfer was 100 USD");
        result.Should().Be("The transfer was 100 dollars");
        result.Should().NotContain("U-S-D");
    }

    [Fact]
    public void Normalize_Should_NotHyphenateUnknownCurrencyCodeAfterAmount()
    {
        // "100 XYZ" is preserved as-is — we don't recognise XYZ as a currency,
        // but we also must not convert it into "100 X-Y-Z" because it is
        // likely an unknown currency code, not a plain acronym.
        SpeechTextNormalizer.Normalize("100 XYZ").Should().Be("100 XYZ");
    }

    [Fact]
    public void Normalize_Should_PreserveLowerCaseAndShortAllCapsWords()
    {
        // Lower-case words and 1-2 letter ALL-CAPS tokens (like "OK", "I", "A")
        // must not be mangled — only 3-5 letter ALL-CAPS tokens are treated as
        // acronyms.
        SpeechTextNormalizer.Normalize("OK I am fine")
            .Should().Be("OK I am fine");
    }

    [Fact]
    public void Normalize_Should_ApplyAllRulesTogether()
    {
        var input = "Hi 👋 the FBI report says APR is 45% on £1,000 GBP — review ASAP!";
        var result = SpeechTextNormalizer.Normalize(input);
        result.Should().Be("Hi the F-B-I report says A-P-R is 45 percent on 1,000 pounds, review A-S-A-P!");
    }
}
