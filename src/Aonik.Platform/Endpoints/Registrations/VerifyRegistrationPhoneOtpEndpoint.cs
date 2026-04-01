using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Aonik.Platform.Contracts.Api.Registrations;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Identity;
using Aonik.SharedKernel.Abstractions;
using FastEndpoints;

namespace Aonik.Platform.Endpoints.Registrations;

internal class VerifyRegistrationPhoneOtpEndpoint : Endpoint<VerifyRegistrationPhoneOtpRequest, VerifyRegistrationPhoneOtpResponse>
{
    private readonly PlatformDbContext _dbContext;
    private readonly IClock _clock;
    private readonly VerificationOptions _options;
    private readonly ILogger<VerifyRegistrationPhoneOtpEndpoint> _logger;

    public VerifyRegistrationPhoneOtpEndpoint(
        PlatformDbContext dbContext,
        IClock clock,
        IOptions<VerificationOptions> options,
        ILogger<VerifyRegistrationPhoneOtpEndpoint> logger)
    {
        _dbContext = dbContext;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("/v1/registrations/phone/verify-otp");
        AllowAnonymous();
    }

    public override async Task HandleAsync(VerifyRegistrationPhoneOtpRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Code))
        {
            await Send.OkAsync(new VerifyRegistrationPhoneOtpResponse(false), ct);
            return;
        }

        var challenge = await _dbContext.PreRegistrationChallenges
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == req.ChallengeId, ct);

        if (challenge == null || challenge.Status != VerificationStatus.Pending)
        {
            await Send.OkAsync(new VerifyRegistrationPhoneOtpResponse(false), ct);
            return;
        }

        if (_clock.UtcNow > challenge.ExpiresAt)
        {
            challenge.Status = VerificationStatus.Expired;
            await _dbContext.SaveChangesAsync(ct);
            await Send.OkAsync(new VerifyRegistrationPhoneOtpResponse(false), ct);
            return;
        }

        challenge.AttemptCount += 1;

        if (challenge.AttemptCount > _options.MaxAttempts)
        {
            challenge.Status = VerificationStatus.Failed;
            await _dbContext.SaveChangesAsync(ct);
            await Send.OkAsync(new VerifyRegistrationPhoneOtpResponse(false), ct);
            return;
        }

        if (!IsCodeMatch(req.Code, challenge.CodeHash))
        {
            await _dbContext.SaveChangesAsync(ct);
            await Send.OkAsync(new VerifyRegistrationPhoneOtpResponse(false), ct);
            return;
        }

        challenge.Status = VerificationStatus.Verified;
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Pre-registration phone verified for challenge {ChallengeId}",
            challenge.Id);

        await Send.OkAsync(new VerifyRegistrationPhoneOtpResponse(true), ct);
    }

    private bool IsCodeMatch(string code, string storedHash)
    {
        var computedHash = HashCode(code);
        return string.Equals(computedHash, storedHash, StringComparison.Ordinal);
    }

    private string HashCode(string code)
    {
        if (string.IsNullOrWhiteSpace(_options.HashKey))
            throw new InvalidOperationException("Verification hashing key is not configured.");

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.HashKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(code));
        return Convert.ToHexString(hash);
    }
}
