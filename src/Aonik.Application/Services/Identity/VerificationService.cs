using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Aonik.Application.Abstractions.Messaging;
using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Models.Identity;
using Aonik.Application.Services.Compliance;
using Aonik.Domain.Identity;
using Aonik.Domain.Identity.Entities;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Application.Services.Identity;

public class VerificationService : IVerificationService
{
    private readonly IAonikDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IEmailSender _emailSender;
    private readonly ISmsSender _smsSender;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly IClock _clock;
    private readonly VerificationOptions _options;
    private readonly ILogger<VerificationService> _logger;

    public VerificationService(
        IAonikDbContext dbContext,
        ITenantProvider tenantProvider,
        IEmailSender emailSender,
        ISmsSender smsSender,
        IAuditLogWriter auditLogWriter,
        IClock clock,
        IOptions<VerificationOptions> options,
        ILogger<VerificationService> logger)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _emailSender = emailSender;
        _smsSender = smsSender;
        _auditLogWriter = auditLogWriter;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    public Task<VerificationChallengeResult> StartEmailVerificationAsync(
        Guid userId,
        string email,
        CancellationToken cancellationToken = default)
    {
        return StartVerificationAsync(
            userId,
            VerificationChannel.Email,
            NormalizeEmail(email),
            cancellationToken);
    }

    public Task<VerificationChallengeResult> StartPhoneVerificationAsync(
        Guid userId,
        string phone,
        CancellationToken cancellationToken = default)
    {
        return StartVerificationAsync(
            userId,
            VerificationChannel.Sms,
            NormalizePhone(phone),
            cancellationToken);
    }

    public Task<bool> ConfirmEmailVerificationAsync(
        Guid userId,
        string email,
        string code,
        CancellationToken cancellationToken = default)
    {
        return ConfirmVerificationAsync(
            userId,
            VerificationChannel.Email,
            NormalizeEmail(email),
            code,
            cancellationToken);
    }

    public Task<bool> ConfirmPhoneVerificationAsync(
        Guid userId,
        string phone,
        string code,
        CancellationToken cancellationToken = default)
    {
        return ConfirmVerificationAsync(
            userId,
            VerificationChannel.Sms,
            NormalizePhone(phone),
            code,
            cancellationToken);
    }

    private async Task<VerificationChallengeResult> StartVerificationAsync(
        Guid userId,
        VerificationChannel channel,
        string target,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(target))
            throw new InvalidOperationException("Verification target is required.");

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
            throw new InvalidOperationException($"User {userId} not found");

        await EnsureWithinRateLimitsAsync(userId, channel, target, cancellationToken);

        var code = GenerateVerificationCode();
        var challenge = new VerificationChallenge
        {
            TenantId = GetTenantId(),
            UserId = userId,
            Channel = channel,
            Target = target,
            CodeHash = HashCode(code),
            ExpiresAt = _clock.UtcNow.AddMinutes(_options.CodeTtlMinutes),
            AttemptCount = 0,
            Status = VerificationStatus.Pending
        };

        _dbContext.VerificationChallenges.Add(challenge);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await SendChallengeAsync(channel, target, code, cancellationToken);

        await _auditLogWriter.LogAsync(
            "VerificationStarted",
            "VerificationChallenge",
            challenge.Id,
            JsonSerializer.Serialize(new
            {
                challenge.Id,
                challenge.UserId,
                challenge.Channel,
                challenge.Target,
                challenge.ExpiresAt
            }),
            cancellationToken);

        _logger.LogInformation(
            "Started verification challenge {ChallengeId} for user {UserId} ({Channel})",
            challenge.Id,
            userId,
            channel);

        return new VerificationChallengeResult(challenge.Id, challenge.ExpiresAt);
    }

    private async Task<bool> ConfirmVerificationAsync(
        Guid userId,
        VerificationChannel channel,
        string target,
        string code,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(target))
            throw new InvalidOperationException("Verification target is required.");

        if (string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("Verification code is required.");

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
            throw new InvalidOperationException($"User {userId} not found");

        var challenge = await _dbContext.VerificationChallenges
            .Where(c => c.UserId == userId && c.Channel == channel && c.Target == target)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (challenge == null || challenge.Status != VerificationStatus.Pending)
        {
            await LogVerificationFailedAsync(userId, channel, target, challenge?.Id, "NoPendingChallenge", cancellationToken);
            return false;
        }

        if (_clock.UtcNow > challenge.ExpiresAt)
        {
            challenge.Status = VerificationStatus.Expired;
            await _dbContext.SaveChangesAsync(cancellationToken);
            await LogVerificationFailedAsync(userId, channel, target, challenge.Id, "Expired", cancellationToken);
            return false;
        }

        if (!IsCodeMatch(code, challenge.CodeHash))
        {
            challenge.AttemptCount += 1;
            if (challenge.AttemptCount >= _options.MaxAttempts)
            {
                challenge.Status = VerificationStatus.Failed;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await LogVerificationFailedAsync(userId, channel, target, challenge.Id, "InvalidCode", cancellationToken);
            return false;
        }

        challenge.Status = VerificationStatus.Verified;

        if (channel == VerificationChannel.Email)
        {
            if (!string.Equals(user.Email, target, StringComparison.OrdinalIgnoreCase))
            {
                user.Email = target;
            }
        }
        else
        {
            if (!string.Equals(user.Phone, target, StringComparison.Ordinal))
            {
                user.Phone = target;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogWriter.LogAsync(
            "VerificationConfirmed",
            "VerificationChallenge",
            challenge.Id,
            JsonSerializer.Serialize(new
            {
                challenge.Id,
                challenge.UserId,
                challenge.Channel,
                challenge.Target
            }),
            cancellationToken);

        _logger.LogInformation(
            "Verified {Channel} for user {UserId} using challenge {ChallengeId}",
            channel,
            userId,
            challenge.Id);

        return true;
    }

    private async Task SendChallengeAsync(
        VerificationChannel channel,
        string target,
        string code,
        CancellationToken cancellationToken)
    {
        if (channel == VerificationChannel.Email)
        {
            var message = new EmailMessage(
                target,
                "Your verification code",
                $"Your verification code is {code}. It expires in {_options.CodeTtlMinutes} minutes.");

            await _emailSender.SendAsync(message, cancellationToken);
        }
        else
        {
            var message = new SmsMessage(
                target,
                $"Your verification code is {code}. It expires in {_options.CodeTtlMinutes} minutes.");

            await _smsSender.SendAsync(message, cancellationToken);
        }
    }

    private async Task EnsureWithinRateLimitsAsync(
        Guid userId,
        VerificationChannel channel,
        string target,
        CancellationToken cancellationToken)
    {
        var windowStart = _clock.UtcNow.AddMinutes(-_options.RateLimits.WindowMinutes);

        var userChannelCount = await _dbContext.VerificationChallenges
            .CountAsync(
                c => c.UserId == userId
                     && c.Channel == channel
                     && c.CreatedAt >= windowStart,
                cancellationToken);

        var targetChannelCount = await _dbContext.VerificationChallenges
            .CountAsync(
                c => c.Channel == channel
                     && c.Target == target
                     && c.CreatedAt >= windowStart,
                cancellationToken);

        if (userChannelCount >= _options.RateLimits.MaxPerUserChannel
            || targetChannelCount >= _options.RateLimits.MaxPerTargetChannel)
        {
            await LogVerificationFailedAsync(
                userId,
                channel,
                target,
                null,
                "RateLimited",
                cancellationToken);

            throw new InvalidOperationException("Verification rate limit exceeded.");
        }
    }

    private async Task LogVerificationFailedAsync(
        Guid userId,
        VerificationChannel channel,
        string target,
        Guid? challengeId,
        string reason,
        CancellationToken cancellationToken)
    {
        await _auditLogWriter.LogAsync(
            "VerificationFailed",
            "VerificationChallenge",
            challengeId ?? Guid.Empty,
            JsonSerializer.Serialize(new
            {
                ChallengeId = challengeId,
                UserId = userId,
                Channel = channel,
                Target = target,
                Reason = reason
            }),
            cancellationToken);
    }

    private Guid GetTenantId()
    {
        if (_tenantProvider.TryGetCurrentTenantId(out var tenantId))
            return tenantId;

        throw new InvalidOperationException("Tenant context is required for verification.");
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

    private static string NormalizeEmail(string email) =>
        email.Trim().ToLowerInvariant();

    private static string NormalizePhone(string phone) =>
        phone.Trim();
}
