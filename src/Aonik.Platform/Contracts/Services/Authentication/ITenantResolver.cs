namespace Aonik.Platform.Contracts.Services.Authentication;

public interface ITenantResolver
{
    Guid? ResolveTenantId();
    Guid? ResolveFromHttpContext();
}
