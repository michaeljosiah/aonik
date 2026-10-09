namespace Aonik.Platform.Contracts.Services.Authentication;

/// <summary>Flow-specific evidence captured only after bearer-token validation and account checks.</summary>
public interface IAccountAccessIdentityProofAccessor
{
    AccountAccessIdentityProof? GetCurrent();
}

public sealed record AccountAccessIdentityProof(
    Guid TenantId,
    string Issuer,
    string Subject,
    string VerifiedEmail,
    DateTime IssuedAtUtc,
    DateTime? AuthenticationTimeUtc,
    Guid? ExistingUserId,
    long? IdentityRevision = null);
