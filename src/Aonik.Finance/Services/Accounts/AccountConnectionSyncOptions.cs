namespace Aonik.Finance.Services.Accounts;

public class AccountConnectionSyncOptions
{
    public bool EnableRecurringSync { get; set; } = true;
    public int DefaultSyncIntervalMinutes { get; set; } = 120;
    public int FailureRetryDelayMinutes { get; set; } = 30;
}
