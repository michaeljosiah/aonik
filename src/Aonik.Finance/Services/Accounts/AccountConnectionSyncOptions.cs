namespace Aonik.Finance.Services.ExternalAccounts;

public class ExternalAccountConnectionSyncOptions
{
    public bool EnableRecurringSync { get; set; } = true;
    public int DefaultSyncIntervalMinutes { get; set; } = 120;
    public int FailureRetryDelayMinutes { get; set; } = 30;
}
