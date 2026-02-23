namespace Aonik.Platform.Contracts.Services.Authentication;

public interface IIdpUserProvisionerFactory
{
    IIdpUserProvisioner GetProvisioner(string provider);
}
