namespace Aonik.Application.Models.Identity;

public record EmailVerificationStartRequest(string Email);

public record PhoneVerificationStartRequest(string Phone);

public record EmailVerificationConfirmRequest(string Email, string Code);

public record PhoneVerificationConfirmRequest(string Phone, string Code);

public record VerificationConfirmationResult(bool IsVerified);
