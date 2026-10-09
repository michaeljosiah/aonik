namespace Aonik.Commerce.Contracts.Models.Fulfilment;

public sealed record BankHolidayDto(DateOnly Date, string Name);

public sealed record BankHolidayPreviewDto(
    string Region,
    string Source,
    IReadOnlyList<BankHolidayDto> Holidays);
