namespace Aonik.Platform.Contracts.Api.Identity;

public record StartEmailVerificationRequest(string Email);

public record StartPhoneVerificationRequest(string Phone);

public record ConfirmEmailVerificationRequest(string Email, string Code);

public record ConfirmPhoneVerificationRequest(string Phone, string Code);

public record VerificationChallengeResponse(
    Guid ChallengeId,
    DateTime ExpiresAt);

public record VerificationConfirmationResponse(bool IsVerified);
