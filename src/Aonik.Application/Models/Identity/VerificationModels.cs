namespace Aonik.Application.Models.Identity;

public record VerificationChallengeResult(
    Guid ChallengeId,
    DateTime ExpiresAt);
