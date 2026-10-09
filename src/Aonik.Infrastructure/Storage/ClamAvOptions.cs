namespace Aonik.Infrastructure.Storage;

internal sealed class ClamAvOptions
{
    public const string SectionName = "ContactEnquiries:ClamAv";

    public bool Enabled { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 3310;
    public int TimeoutSeconds { get; set; } = 10;
}
