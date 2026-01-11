namespace Aonik.Infrastructure.Communication.Configuration;

public class CommunicationOptions
{
    public AzureCommunicationOptions Azure { get; set; } = new();
}

public class AzureCommunicationOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public AzureCommunicationEmailOptions Email { get; set; } = new();
    public AzureCommunicationSmsOptions Sms { get; set; } = new();
}

public class AzureCommunicationEmailOptions
{
    public string FromAddress { get; set; } = string.Empty;
}

public class AzureCommunicationSmsOptions
{
    public string FromPhoneNumber { get; set; } = string.Empty;
}
