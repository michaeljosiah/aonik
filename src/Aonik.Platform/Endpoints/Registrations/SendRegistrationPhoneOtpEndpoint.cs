using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Aonik.Platform.Contracts.Api.Registrations;
using Aonik.Platform.Contracts.Models.Configuration;
using Aonik.Platform.Contracts.Models.Notifications;
using Aonik.Platform.Contracts.Services.Messaging;
using Aonik.Platform.Contracts.Services.Notifications;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Notifications;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Identity;
using Microsoft.Extensions.Hosting;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FastEndpoints;

namespace Aonik.Platform.Endpoints.Registrations;

internal class SendRegistrationPhoneOtpEndpoint : Endpoint<SendRegistrationPhoneOtpRequest, SendRegistrationPhoneOtpResponse>
{
    private readonly PlatformDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly INotificationTemplateService _notificationTemplateService;
    private readonly ISmsSender _smsSender;
    private readonly IClock _clock;
    private readonly VerificationOptions _options;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<SendRegistrationPhoneOtpEndpoint> _logger;

    public SendRegistrationPhoneOtpEndpoint(
        PlatformDbContext dbContext,
        ITenantContext tenantContext,
        INotificationTemplateService notificationTemplateService,
        ISmsSender smsSender,
        IClock clock,
        IOptions<VerificationOptions> options,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        ILogger<SendRegistrationPhoneOtpEndpoint> logger)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _notificationTemplateService = notificationTemplateService;
        _smsSender = smsSender;
        _clock = clock;
        _options = options.Value;
        _configuration = configuration;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("/v1/registrations/phone/send-otp");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Send registration phone OTP";
            s.Description = "Sends a one-time verification code via SMS to the provided phone number for pre-registration verification.";
            s.Response(200, "OTP sent successfully");
            s.Response(400, "Invalid request");
            s.Response(429, "Rate limit exceeded");
        });
        Options(x => x.WithTags("Registration"));
    }

    public override async Task HandleAsync(SendRegistrationPhoneOtpRequest req, CancellationToken ct)
    {
        var phone = req.Phone?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(phone))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "Phone number is required." }, ct);
            return;
        }

        var tenantId = await ResolveTenantIdAsync(req, ct);
        if (!tenantId.HasValue)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "TenantId is required." }, ct);
            return;
        }

        _tenantContext.TenantId = tenantId.Value;
        _tenantContext.ResolutionSource = "Registration";

        if (!await IsWithinRateLimitAsync(tenantId.Value, phone, ct))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "Too many verification attempts. Please try again later." }, ct);
            return;
        }

        var code = GenerateVerificationCode();
        var challenge = new PreRegistrationChallenge
        {
            TenantId = tenantId.Value,
            Phone = phone,
            CodeHash = HashCode(code),
            ExpiresAt = _clock.UtcNow.AddMinutes(_options.CodeTtlMinutes),
            AttemptCount = 0,
            Status = VerificationStatus.Pending
        };

        _dbContext.PreRegistrationChallenges.Add(challenge);
        await _dbContext.SaveChangesAsync(ct);

        await SendSmsAsync(phone, code, ct);

        _logger.LogInformation(
            "Pre-registration phone OTP sent for challenge {ChallengeId}",
            challenge.Id);

        var devCode = _hostEnvironment.IsDevelopment()
            || _hostEnvironment.EnvironmentName == "dev"
            ? code
            : null;

        await Send.OkAsync(new SendRegistrationPhoneOtpResponse(challenge.Id, challenge.ExpiresAt, devCode), ct);
    }

    private async Task<bool> IsWithinRateLimitAsync(Guid tenantId, string phone, CancellationToken ct)
    {
        var windowStart = _clock.UtcNow.AddMinutes(-_options.RateLimits.WindowMinutes);

        var recentCount = await _dbContext.PreRegistrationChallenges
            .IgnoreQueryFilters()
            .CountAsync(
                c => c.TenantId == tenantId
                     && c.Phone == phone
                     && c.CreatedAt >= windowStart,
                ct);

        return recentCount < _options.RateLimits.MaxPerTargetChannel;
    }

    private async Task SendSmsAsync(string phone, string code, CancellationToken ct)
    {
        var model = new Dictionary<string, object?>
        {
            ["otp_code"] = code,
            ["expiry_minutes"] = _options.CodeTtlMinutes
        };

        var rendered = await _notificationTemplateService.RenderAsync(
            new RenderNotificationTemplateRequest(NotificationTemplateNames.SmsOtp, "SMS", model),
            ct);

        await _smsSender.SendAsync(new SmsMessage(phone, rendered.Body), ct);
    }

    private string GenerateVerificationCode()
    {
        var length = _options.CodeLength <= 0 ? 6 : _options.CodeLength;
        Span<byte> data = stackalloc byte[length];
        RandomNumberGenerator.Fill(data);
        var chars = new char[length];

        for (var i = 0; i < length; i += 1)
        {
            chars[i] = (char)('0' + (data[i] % 10));
        }

        return new string(chars);
    }

    private string HashCode(string code)
    {
        if (string.IsNullOrWhiteSpace(_options.HashKey))
            throw new InvalidOperationException("Verification hashing key is not configured.");

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.HashKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(code));
        return Convert.ToHexString(hash);
    }

    private async Task<Guid?> ResolveTenantIdAsync(SendRegistrationPhoneOtpRequest request, CancellationToken ct)
    {
        if (request.TenantId.HasValue && request.TenantId.Value != Guid.Empty)
        {
            return request.TenantId.Value;
        }

        var mode = _configuration.GetValue<TenantRoutingMode>("Auth:TenantRouting");
        return mode switch
        {
            TenantRoutingMode.Subdomain => await ResolveFromSubdomainAsync(ct),
            TenantRoutingMode.Header => ResolveFromHeader(),
            _ => null
        };
    }

    private async Task<Guid?> ResolveFromSubdomainAsync(CancellationToken ct)
    {
        var host = HttpContext.Request.Host.Host;
        var parts = host.Split('.');
        if (parts.Length < 3)
        {
            return null;
        }

        var subdomain = parts[0];
        return await _dbContext.Tenants
            .Where(t => t.Subdomain == subdomain && t.Status == "Active")
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync(ct);
    }

    private Guid? ResolveFromHeader()
    {
        var header = HttpContext.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        return Guid.TryParse(header, out var tenantId) ? tenantId : null;
    }
}
