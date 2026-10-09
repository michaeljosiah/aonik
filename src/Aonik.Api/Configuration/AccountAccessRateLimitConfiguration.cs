using System.Globalization;
using System.Threading.RateLimiting;

using Aonik.SharedKernel.Abstractions.Multitenancy;

using Microsoft.AspNetCore.RateLimiting;

namespace Aonik.Api.Configuration;

public static class AccountAccessRateLimitConfiguration
{
    public static IServiceCollection AddAccountAccessRateLimit(this IServiceCollection services, IConfiguration configuration)
    {
        return services.AddRateLimiter(options =>
        {
            var permits = configuration.GetValue("Identity:SecurityRequestsPerMinute", 30);
            if (permits < 1)
                throw new InvalidOperationException("Identity:SecurityRequestsPerMinute must be positive.");

            options.AddPolicy("identity-security", context =>
            {
                var tenantId = context.RequestServices.GetRequiredService<ITenantContext>().TenantId;
                var address = context.Connection.RemoteIpAddress;
                if (address?.IsIPv4MappedToIPv6 == true) address = address.MapToIPv4();
                return RateLimitPartition.GetFixedWindowLimiter($"{tenantId:N}:{address?.ToString() ?? "unknown"}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permits,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });

            // Keep the delivery policy's existing rejection contract. Identity requests never
            // disclose whether an address/action exists, including when throttled.
            var previousRejection = options.OnRejected;
            options.OnRejected = async (context, ct) =>
            {
                var policy = context.HttpContext.GetEndpoint()?.Metadata
                    .GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;
                if (policy != "identity-security")
                {
                    if (previousRejection is not null) await previousRejection(context, ct);
                    return;
                }

                var response = context.HttpContext.Response;
                response.Headers.CacheControl = "no-store";
                response.Headers["Referrer-Policy"] = "no-referrer";
                var path = context.HttpContext.Request.Path;
                if (path.Equals("/identity/account-access/resend", StringComparison.OrdinalIgnoreCase)
                    || path.Equals("/profiles/customers/me/email", StringComparison.OrdinalIgnoreCase))
                {
                    response.StatusCode = StatusCodes.Status202Accepted;
                    return;
                }
                if (path.Equals("/identity/password/forgot", StringComparison.OrdinalIgnoreCase))
                {
                    response.StatusCode = StatusCodes.Status200OK;
                    await response.WriteAsJsonAsync(new { status = "ok" }, ct);
                    return;
                }

                var retrySeconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                    ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)) : 60;
                response.StatusCode = StatusCodes.Status429TooManyRequests;
                response.Headers.RetryAfter = retrySeconds.ToString(CultureInfo.InvariantCulture);
            };
        });
    }
}
