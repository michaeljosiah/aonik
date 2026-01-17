namespace Aonik.Application.Abstractions.Authentication;

public interface IAuthTokenServiceFactory
{
    IAuthTokenService GetService(string provider);
}
