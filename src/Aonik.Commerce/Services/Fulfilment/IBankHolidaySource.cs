using Aonik.Commerce.Contracts.Models.Fulfilment;

namespace Aonik.Commerce.Services.Fulfilment;

public interface IBankHolidaySource
{
    /// <summary>Returns the official dates for review, or null when the source is unavailable or invalid.</summary>
    Task<BankHolidayPreviewDto?> GetAsync(string region, CancellationToken cancellationToken = default);
}
