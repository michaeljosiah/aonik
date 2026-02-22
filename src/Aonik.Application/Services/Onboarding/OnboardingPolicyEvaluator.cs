using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Models.Onboarding;
using Aonik.Application.Options;
using Aonik.Platform.Entities.Identity;

namespace Aonik.Application.Services.Onboarding;

public class OnboardingPolicyEvaluator : IOnboardingPolicyEvaluator
{
    private readonly IAonikDbContext _dbContext;
    private readonly OnboardingPolicyOptions _options;

    public OnboardingPolicyEvaluator(
        IAonikDbContext dbContext,
        IOptions<OnboardingPolicyOptions> options)
    {
        _dbContext = dbContext;
        _options = options.Value;
    }

    public async Task<OnboardingSnapshot> EvaluateAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
        {
            throw new InvalidOperationException($"User {userId} not found.");
        }

        var partyId = await GetPrimaryPartyIdAsync(userId, cancellationToken);
        var emailVerified = await IsEmailVerifiedAsync(user, cancellationToken);
        var phoneVerified = await IsPhoneVerifiedAsync(user, cancellationToken);
        var profileComplete = await IsProfileCompleteAsync(partyId, cancellationToken);

        var gates = new List<OnboardingGateStatus>
        {
            new(
                OnboardingGate.EmailVerified,
                emailVerified,
                _options.RequireEmailVerified,
                NormalizeActions(_options.EmailVerifiedActions)),
            new(
                OnboardingGate.PhoneVerified,
                phoneVerified,
                _options.RequirePhoneVerified,
                NormalizeActions(_options.PhoneVerifiedActions)),
            new(
                OnboardingGate.ProfileComplete,
                profileComplete,
                _options.RequireProfileComplete,
                NormalizeActions(_options.ProfileCompleteActions))
        };

        var nextActions = gates
            .Where(gate => gate.IsRequired && !gate.IsSatisfied)
            .SelectMany(gate => gate.RequiredActions)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new OnboardingSnapshot(userId, partyId, gates, nextActions);
    }

    private async Task<Guid?> GetPrimaryPartyIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _dbContext.UserParties
            .Where(link => link.UserId == userId)
            .OrderByDescending(link => link.CreatedAt)
            .Select(link => (Guid?)link.PartyId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<bool> IsEmailVerifiedAsync(User user, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return false;
        }

        var normalizedEmail = NormalizeEmail(user.Email);

        return await _dbContext.VerificationChallenges
            .AnyAsync(
                challenge => challenge.UserId == user.Id
                             && challenge.Channel == VerificationChannel.Email
                             && challenge.Status == VerificationStatus.Verified
                             && challenge.Target == normalizedEmail,
                cancellationToken);
    }

    private async Task<bool> IsPhoneVerifiedAsync(User user, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(user.Phone))
        {
            return false;
        }

        var normalizedPhone = NormalizePhone(user.Phone);

        return await _dbContext.VerificationChallenges
            .AnyAsync(
                challenge => challenge.UserId == user.Id
                             && challenge.Channel == VerificationChannel.Sms
                             && challenge.Status == VerificationStatus.Verified
                             && challenge.Target == normalizedPhone,
                cancellationToken);
    }

    private async Task<bool> IsProfileCompleteAsync(Guid? partyId, CancellationToken cancellationToken)
    {
        if (!partyId.HasValue)
        {
            return false;
        }

        var party = await _dbContext.Parties
            .FirstOrDefaultAsync(p => p.Id == partyId.Value, cancellationToken);


        if (party == null || string.IsNullOrWhiteSpace(party.DisplayName))
        {
            return false;
        }

        var hasAddress = await _dbContext.PartyAddresses
            .AnyAsync(
                address => address.PartyId == partyId.Value
                           && !string.IsNullOrWhiteSpace(address.Line1)
                           && !string.IsNullOrWhiteSpace(address.City)
                           && !string.IsNullOrWhiteSpace(address.Country),
                cancellationToken);

        if (!hasAddress)
        {
            return false;
        }

        var hasContact = await _dbContext.PartyContacts
            .AnyAsync(
                contact => contact.PartyId == partyId.Value
                           && !string.IsNullOrWhiteSpace(contact.Value),
                cancellationToken);

        return hasContact;
    }

    private static IReadOnlyList<string> NormalizeActions(IEnumerable<string> actions)
    {
        return actions
            .Where(action => !string.IsNullOrWhiteSpace(action))
            .Select(action => action.Trim())
            .ToList();
    }

    private static string NormalizeEmail(string email) =>
        email.Trim().ToLowerInvariant();

    private static string NormalizePhone(string phone) =>
        phone.Trim();
}
