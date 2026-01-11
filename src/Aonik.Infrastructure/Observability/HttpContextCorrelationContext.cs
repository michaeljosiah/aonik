using Aonik.Application.Abstractions.Observability;
using Microsoft.AspNetCore.Http;

namespace Aonik.Infrastructure.Observability;

public class HttpContextCorrelationContext : ICorrelationContext
{
    private const string CorrelationHeaderName = "X-Correlation-Id";
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextCorrelationContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? CorrelationId
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                return null;
            }

            var headerValue = httpContext.Request.Headers[CorrelationHeaderName].ToString();
            if (!string.IsNullOrWhiteSpace(headerValue))
            {
                return headerValue;
            }

            return httpContext.TraceIdentifier;
        }
    }
}
