namespace Aonik.PersonalFinance.Services;

internal sealed class FinancialConnectionSyncOptions
{
    public bool EnableRecurringSync { get; set; } = true;

    public int DefaultSyncIntervalMinutes { get; set; } = 360;

    public int WorkerPollIntervalSeconds { get; set; } = 60;

    public int BatchSize { get; set; } = 25;

    public int FailureRetryDelayMinutes { get; set; } = 15;
}
