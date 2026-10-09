namespace Aonik.Infrastructure.Authentication.Configuration;

/// <summary>Claim names populated by the trusted identity provider, never by application request data.</summary>
public sealed class AccountAccessProofOptions
{
    public string EmailClaimType { get; set; } = "email";
    public string EmailVerifiedClaimType { get; set; } = "email_verified";
    public string AuthenticationTimeClaimType { get; set; } = "auth_time";
}
