using System.Globalization;
using System.Threading.RateLimiting;

using Aonik.Commerce.Endpoints.Public.Fulfilment;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Api.Configuration;

public static class DeliveryCoverageRateLimitConfiguration
{
    public static IServiceCollection AddDeliveryCoverageRateLimit(this IServiceCollection services, IConfiguration configuration)
    {
        return services.AddRateLimiter(options =>
        {
            var permits = configuration.GetValue("Commerce:DeliveryCoverage:RequestsPerMinute", 30);
            if (permits < 1)
                throw new InvalidOperationException("Commerce:DeliveryCoverage:RequestsPerMinute must be positive.");

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(GetDeliveryCoverageEndpoint.RateLimitPolicyName, context =>
            {
                var tenantId = context.RequestServices.GetRequiredService<ITenantContext>().TenantId;
                var address = context.Connection.RemoteIpAddress;
                if (address?.IsIPv4MappedToIPv6 == true) address = address.MapToIPv4();

                // Tenant validation and trusted forwarded headers have already run. Never partition
                // on a caller's raw tenant/IP headers, query values or an invented per-request key.
                return RateLimitPartition.GetFixedWindowLimiter($"{tenantId:N}:{address?.ToString() ?? "unknown"}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permits,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });
            options.OnRejected = async (context, ct) =>
            {
                var retrySeconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                    ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)) : 60;
                var response = context.HttpContext.Response;
                response.StatusCode = StatusCodes.Status429TooManyRequests;
                response.Headers.CacheControl = "no-store";
                response.Headers.RetryAfter = retrySeconds.ToString(CultureInfo.InvariantCulture);
                await response.WriteAsJsonAsync(new
                {
                    code = "commerce.delivery_rate_limited",
                    error = "Too many delivery checks. Please try again later."
                }, ct);
            };
        });
    }
}
