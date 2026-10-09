using System.Globalization;
using System.Threading.RateLimiting;

using Aonik.Platform.Endpoints.ContactEnquiries;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using Microsoft.AspNetCore.RateLimiting;

namespace Aonik.Api.Configuration;

public static class ContactEnquiryRateLimitConfiguration
{
    public static IServiceCollection AddContactEnquiryRateLimit(this IServiceCollection services, IConfiguration configuration)
        => services.AddRateLimiter(options =>
        {
            var permits = configuration.GetValue("ContactEnquiries:RequestsPerMinute", 10);
            if (permits < 1) throw new InvalidOperationException("ContactEnquiries:RequestsPerMinute must be positive.");
            options.AddPolicy(SubmitContactEnquiryEndpoint.RateLimitPolicyName, context =>
            {
                var tenantId = context.RequestServices.GetRequiredService<ITenantContext>().TenantId;
                var address = context.Connection.RemoteIpAddress;
                if (address?.IsIPv4MappedToIPv6 == true) address = address.MapToIPv4();
                // Forwarded headers are useful only after the host's trusted-proxy handling.
                // A BFF without that topology shares an aggregate budget at its own address.
                return RateLimitPartition.GetFixedWindowLimiter($"{tenantId:N}:{address?.ToString() ?? "unknown"}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permits, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true
                    });
            });
            var previous = options.OnRejected;
            options.OnRejected = async (context, ct) =>
            {
                if (context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName
                    != SubmitContactEnquiryEndpoint.RateLimitPolicyName)
                {
                    if (previous is not null) await previous(context, ct);
                    return;
                }
                var response = context.HttpContext.Response;
                response.StatusCode = StatusCodes.Status429TooManyRequests;
                response.Headers.CacheControl = "no-store";
                response.Headers.RetryAfter = (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var after)
                    ? Math.Max(1, (int)Math.Ceiling(after.TotalSeconds)) : 60).ToString(CultureInfo.InvariantCulture);
                await response.WriteAsJsonAsync(new { code = "contact.rate_limited", error = "Too many enquiries. Please try again later." }, ct);
            };
        });
}
