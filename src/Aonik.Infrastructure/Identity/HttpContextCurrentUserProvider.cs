using System.Security.Claims;
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
        var userIdClaim = _httpContextAccessor.HttpContext?.User
            .FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim))
            return null;

        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    public bool TryGetCurrentUserId(out Guid userId)
    {
        var result = GetCurrentUserId();
        userId = result ?? Guid.Empty;
        return result.HasValue;
    }
}
