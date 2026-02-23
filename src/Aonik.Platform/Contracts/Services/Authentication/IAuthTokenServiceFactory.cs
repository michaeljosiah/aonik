namespace Aonik.Platform.Contracts.Services.Authentication;

public interface IAuthTokenServiceFactory
{
    IAuthTokenService GetService(string provider);
}
