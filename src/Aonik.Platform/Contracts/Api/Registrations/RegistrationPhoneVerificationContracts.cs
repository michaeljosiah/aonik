namespace Aonik.Platform.Contracts.Api.Registrations;

public record SendRegistrationPhoneOtpRequest(Guid? TenantId, string Phone);

public record SendRegistrationPhoneOtpResponse(Guid ChallengeId, DateTime ExpiresAt);

public record VerifyRegistrationPhoneOtpRequest(Guid ChallengeId, string Code);

public record VerifyRegistrationPhoneOtpResponse(bool IsVerified);
