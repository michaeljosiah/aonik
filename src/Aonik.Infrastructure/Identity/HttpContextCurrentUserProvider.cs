using Aonik.SharedKernel.Abstractions;

namespace Aonik.Infrastructure.Identity;

public class HttpContextCurrentUserProvider : ICurrentUserProvider
{
    private readonly ICurrentUserContext _currentUserContext;

    public HttpContextCurrentUserProvider(ICurrentUserContext currentUserContext)
    {
        _currentUserContext = currentUserContext;
    }

    public Guid? GetCurrentUserId()
    {
        return _currentUserContext.UserId;
    }

    public bool TryGetCurrentUserId(out Guid userId)
    {
        var id = GetCurrentUserId();
        userId = id ?? Guid.Empty;
        return id.HasValue;
    }
}
