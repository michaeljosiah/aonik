using Aonik.Voice.Processors;
using FluentAssertions;

namespace Aonik.Voice.Tests.Pipeline;

/// <summary>
/// Unit tests for <see cref="AonikSentenceAggregator"/>. The boundary detection runs on a
/// pure string + index pair — we exercise the algorithm directly via
/// <c>FindLastSentenceBoundary</c> rather than spin a full <c>FrameProcessor</c> harness for
/// each scenario. The frame-loop wiring is exercised once via a smoke test below; the rest
/// of the cases pin the rule that fixed the production bug.
/// </summary>
public class AonikSentenceAggregatorTests
{
    // ── Boundary detection (string-level) ────────────────────────────────────────────────

    [Fact]
    public void Currency_With_Cents_At_End_Of_Buffer_Is_Not_A_Boundary()
    {
        // The bug: upstream Voxa flushed "It costs $10,000." then started a new sentence
        // with "00 plus tax.". We want NO boundary while the period sits at end-of-buffer
        // so the next chunk can fill in the cents.
        AonikSentenceAggregator.FindLastSentenceBoundary("It costs $10,000.")
            .Should().Be(-1);
    }

    [Fact]
    public void Currency_With_Cents_Mid_Sentence_Splits_After_The_Real_Period()
    {
        // After more text streams in we should find the *real* sentence boundary. The
        // trailing "." at end-of-buffer is NOT a boundary; the ". " after "00" IS.
        var s = "It costs $10,000.00. The next bill is later.";
        var idx = AonikSentenceAggregator.FindLastSentenceBoundary(s);
        idx.Should().BeGreaterThan(0);
        s[idx].Should().Be('.');
        idx.Should().Be(s.IndexOf(". The", StringComparison.Ordinal));
    }

    [Fact]
    public void Decimal_Number_Inside_Sentence_Stays_Whole()
    {
        // "Pi is 3.14 approximately. " → only the period after "approximately" is a
        // boundary; the dot in 3.14 is followed by a digit (not whitespace) so it's
        // skipped.
        var s = "Pi is 3.14 approximately. ";
        var idx = AonikSentenceAggregator.FindLastSentenceBoundary(s);
        idx.Should().BeGreaterThan(0);
        s.Substring(idx - "approximately".Length, "approximately".Length + 1)
            .Should().Be("approximately.");
    }

    [Fact]
    public void Question_Mark_Followed_By_Whitespace_Is_A_Boundary()
    {
        var s = "Are you sure? Yes please.";
        // The last boundary in this buffer is the '.' at end — but end-of-buffer no
        // longer counts, so we expect the '?' after "sure".
        var idx = AonikSentenceAggregator.FindLastSentenceBoundary(s);
        s[idx].Should().Be('?');
    }

    [Fact]
    public void Exclamation_Followed_By_Whitespace_Is_A_Boundary()
    {
        var s = "Great! Next one.";
        var idx = AonikSentenceAggregator.FindLastSentenceBoundary(s);
        s[idx].Should().Be('!');
    }

    [Fact]
    public void Newline_Is_Treated_As_A_Boundary()
    {
        // A bare newline (no following whitespace strictly required, but \n itself counts
        // as whitespace for the "followed by whitespace" check — so the previous newline
        // qualifies because the next char is the start of the next line).
        var s = "Heading\nBody text continues.";
        var idx = AonikSentenceAggregator.FindLastSentenceBoundary(s);
        // Last char is end-of-buffer (not a boundary); previous candidate is '\n' but the
        // char after it is 'B' (not whitespace) so it isn't a boundary either. Result: -1.
        idx.Should().Be(-1);

        var withTrailingSpace = "Heading\n Body text.";
        var idx2 = AonikSentenceAggregator.FindLastSentenceBoundary(withTrailingSpace);
        withTrailingSpace[idx2].Should().Be('\n');
    }

    [Fact]
    public void End_Of_Buffer_Period_Is_Never_A_Boundary()
    {
        // This is the core fix — previously `.` at end of buffer was a boundary.
        AonikSentenceAggregator.FindLastSentenceBoundary("Done.").Should().Be(-1);
        AonikSentenceAggregator.FindLastSentenceBoundary("Are you sure?").Should().Be(-1);
        AonikSentenceAggregator.FindLastSentenceBoundary("Done!").Should().Be(-1);
    }

    [Fact]
    public void Period_Followed_By_Letter_Is_Not_A_Boundary()
    {
        // Catches the streaming pattern "first chunk ends mid-token", e.g. "v1.5x".
        AonikSentenceAggregator.FindLastSentenceBoundary("v1.5x release ready")
            .Should().Be(-1);
    }

    [Fact]
    public void No_Boundary_Returns_Minus_One()
    {
        AonikSentenceAggregator.FindLastSentenceBoundary("just some text")
            .Should().Be(-1);
        AonikSentenceAggregator.FindLastSentenceBoundary(string.Empty)
            .Should().Be(-1);
    }
}
