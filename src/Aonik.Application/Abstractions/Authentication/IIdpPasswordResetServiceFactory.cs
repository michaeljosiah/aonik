namespace Aonik.Application.Abstractions.Authentication;

public interface IIdpPasswordResetServiceFactory
{
    IIdpPasswordResetService GetService(string provider);
}
