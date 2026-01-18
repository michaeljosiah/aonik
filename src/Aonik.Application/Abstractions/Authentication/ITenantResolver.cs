using System;

namespace Aonik.Application.Abstractions.Authentication;

public interface ITenantResolver
{
    Guid? ResolveTenantId();
    Guid? ResolveFromHttpContext();
}
