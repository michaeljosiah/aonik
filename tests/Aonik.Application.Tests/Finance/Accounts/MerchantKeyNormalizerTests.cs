using Aonik.Finance.Services.Accounts;
using FluentAssertions;

namespace Aonik.Application.Tests.Finance.Accounts;

public class MerchantKeyNormalizerTests
{
    [Fact]
    public void Normalize_Should_LowerAndTrim()
    {
        MerchantKeyNormalizer.Normalize("  Spotify AB  ")
            .Should().Be("spotify ab");
    }

    [Theory]
    [InlineData("Acme Coffee Ltd", "acme coffee")]
    [InlineData("Initech Limited", "initech")]
    [InlineData("Globex Inc", "globex")]
    [InlineData("Hooli plc", "hooli")]
    [InlineData("Pied Piper LLC", "pied piper")]
    [InlineData("Soylent Corp", "soylent")]
    [InlineData("Massive Dynamic Corporation", "massive dynamic")]
    public void Normalize_Should_StripNoiseSuffixes(string input, string expected)
    {
        MerchantKeyNormalizer.Normalize(input).Should().Be(expected);
    }

    [Fact]
    public void Normalize_Should_ReplaceNonAlphanumericWithSpace_AndCollapseWhitespace()
    {
        MerchantKeyNormalizer.Normalize("NETFLIX.COM").Should().Be("netflix com");
    }

    [Fact]
    public void Normalize_Should_PreserveHyphens()
    {
        MerchantKeyNormalizer.Normalize("uber-eats")
            .Should().Be("uber-eats");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("&&&")]
    [InlineData("$$$")]
    public void Normalize_Should_ReturnNull_When_Empty_Or_OnlyPunctuation(string? input)
    {
        MerchantKeyNormalizer.Normalize(input).Should().BeNull();
    }

    [Fact]
    public void Normalize_Should_TruncateAt200Chars()
    {
        var raw = new string('a', 500);
        var result = MerchantKeyNormalizer.Normalize(raw);
        result.Should().NotBeNull();
        result!.Length.Should().Be(200);
    }
}
