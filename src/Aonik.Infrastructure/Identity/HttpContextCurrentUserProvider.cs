using Aonik.SharedKernel.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Aonik.Infrastructure.Identity;

public class HttpContextCurrentUserProvider : ICurrentUserProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextCurrentUserProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? GetCurrentUserId()
    {
        // Read from HttpContext.Items (populated by OnTokenValidated)
        return _httpContextAccessor.HttpContext?.Items["AonikUserId"] as Guid?;
    }

    public bool TryGetCurrentUserId(out Guid userId)
    {
        var id = GetCurrentUserId();
        userId = id ?? Guid.Empty;
        return id.HasValue;
    }
}
