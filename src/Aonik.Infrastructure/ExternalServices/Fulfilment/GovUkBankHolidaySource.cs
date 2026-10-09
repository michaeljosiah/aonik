using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Fulfilment;
using Aonik.Commerce.Services.Fulfilment;

namespace Aonik.Infrastructure.ExternalServices.Fulfilment;

internal sealed class GovUkBankHolidaySource(HttpClient httpClient) : IBankHolidaySource
{
    internal const string SourceUrl = "https://www.gov.uk/bank-holidays.json";
    internal const long MaxResponseBytes = 256 * 1024;
    internal static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

    public async Task<BankHolidayPreviewDto?> GetAsync(
        string region, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!BankHolidayRegions.IsSupported(region))
            throw new ArgumentException("A supported UK bank-holiday region is required.", nameof(region));

        try
        {
            // Buffering keeps both headers and body within the client's timeout and byte limit.
            using var response = await httpClient.GetAsync(SourceUrl, cancellationToken);
            if (response.StatusCode != HttpStatusCode.OK)
                return null;

            var regions = await response.Content.ReadFromJsonAsync<Dictionary<string, HolidayDivision?>>(cancellationToken);
            if (regions is null || !regions.TryGetValue(region, out var division)
                || division?.Division != region || division.Events is not { Count: > 0 })
                return null;

            var holidays = new List<BankHolidayDto>(division.Events.Count);
            foreach (var holiday in division.Events)
            {
                if (holiday is null || string.IsNullOrWhiteSpace(holiday.Title)
                    || !DateOnly.TryParseExact(holiday.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var date))
                    return null;

                holidays.Add(new BankHolidayDto(date, holiday.Title.Trim()));
            }

            return new BankHolidayPreviewDto(region, SourceUrl,
                holidays.Distinct().OrderBy(holiday => holiday.Date).ThenBy(holiday => holiday.Name, StringComparer.Ordinal).ToArray());
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record HolidayDivision(string? Division, List<HolidayEvent?>? Events);
    private sealed record HolidayEvent(string? Title, string? Date);
}
