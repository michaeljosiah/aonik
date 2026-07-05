using Aonik.Finance.Contracts.Services.Pricing;
using Aonik.Finance.Services.Finance.Readers;
using FluentAssertions;
using Moq;

namespace Aonik.Application.Tests.Finance.Readers;

/// <summary>
/// Tests for <see cref="FxRateHistoryReader"/> — the SharedKernel FX rate-history contract that
/// lets PersonalFinance's Simi FX tool read a rate series without depending on Finance's pricing
/// service (Spec 027 S-Contracts / #118). It is a faithful projection of the pricing service's
/// <c>FxRateHistoryResult</c>.
/// </summary>
public class FxRateHistoryReaderTests
{
    [Fact]
    public async Task GetRateHistoryAsync_Should_PassThroughArguments_And_ProjectResult()
    {
        var fxRateService = new Mock<IFxRateService>(MockBehavior.Strict);
        fxRateService
            .Setup(s => s.GetRateHistoryAsync("GBP", "NGN", 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FxRateHistoryResult(
                BaseCurrency: "GBP",
                TargetCurrency: "NGN",
                Rates:
                [
                    new FxRatePoint("2026-01-01", 2050m),
                    new FxRatePoint("2026-01-02", 2075m),
                ],
                Signal: "buy",
                SignalReason: "Rate trending up"));

        var reader = new FxRateHistoryReader(fxRateService.Object);

        var history = await reader.GetRateHistoryAsync("GBP", "NGN", 7);

        history.BaseCurrency.Should().Be("GBP");
        history.TargetCurrency.Should().Be("NGN");
        history.Signal.Should().Be("buy");
        history.SignalReason.Should().Be("Rate trending up");
        history.Rates.Should().HaveCount(2);
        history.Rates[0].Date.Should().Be("2026-01-01");
        history.Rates[0].Rate.Should().Be(2050m);
        history.Rates[1].Date.Should().Be("2026-01-02");
        history.Rates[1].Rate.Should().Be(2075m);

        fxRateService.Verify(s => s.GetRateHistoryAsync("GBP", "NGN", 7, It.IsAny<CancellationToken>()), Times.Once);
    }
}
