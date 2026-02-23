namespace Aonik.Platform.Contracts.Services.Authentication;

public interface IIdpPasswordResetServiceFactory
{
    IIdpPasswordResetService GetService(string provider);
}
