namespace Aonik.Platform.Contracts.Services.Authentication;

public interface IIdpAccountServiceFactory
{
    IIdpAccountService GetService(string provider);
}
