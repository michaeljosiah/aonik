namespace Aonik.Application.Abstractions.Authentication;

public interface IIdpUserProvisionerFactory
{
    IIdpUserProvisioner GetProvisioner(string provider);
}
