using System.Globalization;
using System.Threading.RateLimiting;

using Microsoft.AspNetCore.RateLimiting;

using Aonik.Commerce.Endpoints.Public.Checkout;
using Aonik.Commerce.Endpoints.Public.Fulfilment;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Api.Configuration;

public static class DeliveryCoverageRateLimitConfiguration
{
    public static IServiceCollection AddDeliveryCoverageRateLimit(this IServiceCollection services, IConfiguration configuration)
    {
        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            AddPolicy(GetDeliveryCoverageEndpoint.RateLimitPolicyName, "Commerce:DeliveryCoverage:RequestsPerMinute", 30);
            AddPolicy(CheckoutRateLimitPolicies.Checkout, "Commerce:Checkout:RequestsPerMinute", 30);
            AddPolicy(CheckoutRateLimitPolicies.PaymentStatus, "Commerce:PaymentStatus:RequestsPerMinute", 300);

            void AddPolicy(string name, string setting, int defaultPermits)
            {
                var permits = configuration.GetValue(setting, defaultPermits);
                if (permits < 1) throw new InvalidOperationException($"{setting} must be positive.");
                options.AddPolicy(name, context =>
                {
                    var tenantId = context.RequestServices.GetRequiredService<ITenantContext>().TenantId;
                    var address = context.Connection.RemoteIpAddress;
                    if (address?.IsIPv4MappedToIPv6 == true) address = address.MapToIPv4();

                    // Tenant validation and trusted forwarded headers have already run. Never partition
                    // on a caller's raw tenant/IP headers, query values or an invented per-request key.
                    return RateLimitPartition.GetFixedWindowLimiter($"{name}:{tenantId:N}:{address?.ToString() ?? "unknown"}",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = permits,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0,
                            AutoReplenishment = true
                        });
                });
            }
            options.OnRejected = async (context, ct) =>
            {
                var retrySeconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                    ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)) : 60;
                var response = context.HttpContext.Response;
                response.StatusCode = StatusCodes.Status429TooManyRequests;
                response.Headers.CacheControl = "no-store";
                response.Headers.RetryAfter = retrySeconds.ToString(CultureInfo.InvariantCulture);
                response.Headers["Referrer-Policy"] = "no-referrer";
                var policy = context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;
                var (code, error) = policy switch
                {
                    CheckoutRateLimitPolicies.Checkout => ("commerce.checkout_rate_limited", "Too many checkout attempts. Please try again later."),
                    CheckoutRateLimitPolicies.PaymentStatus => ("commerce.payment_status_rate_limited", "Too many payment checks. Please try again later."),
                    _ => ("commerce.delivery_rate_limited", "Too many delivery checks. Please try again later.")
                };
                await response.WriteAsJsonAsync(new
                {
                    code,
                    error
                }, ct);
            };
        });
    }
}
