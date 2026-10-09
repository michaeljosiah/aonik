namespace Aonik.Commerce.Services.Fulfilment;

public static class BankHolidayRegions
{
    public static bool IsSupported(string? region) =>
        region is "england-and-wales" or "scotland" or "northern-ireland";
}
