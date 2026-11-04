namespace Aonik.Publishing.Services;

public sealed class PublishingException(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}
