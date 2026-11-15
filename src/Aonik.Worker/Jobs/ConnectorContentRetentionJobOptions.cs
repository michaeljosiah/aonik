namespace Aonik.Worker.Jobs;

public sealed class ConnectorContentRetentionJobOptions
{
    public bool Enabled { get; set; } = true;
    public string CronExpression { get; set; } = "0 23 * * * ?";
}
