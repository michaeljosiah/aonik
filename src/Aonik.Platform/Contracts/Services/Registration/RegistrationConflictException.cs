namespace Aonik.Platform.Contracts.Services.Registration;

public sealed class RegistrationConflictException : InvalidOperationException
{
    public RegistrationConflictException(string message)
        : base(message)
    {
    }

    public RegistrationConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
