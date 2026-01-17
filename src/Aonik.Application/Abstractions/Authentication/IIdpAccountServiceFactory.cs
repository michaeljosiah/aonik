namespace Aonik.Application.Abstractions.Authentication;

public interface IIdpAccountServiceFactory
{
    IIdpAccountService GetService(string provider);
}
