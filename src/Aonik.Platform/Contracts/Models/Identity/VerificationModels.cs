namespace Aonik.Platform.Contracts.Models.Identity;

public record VerificationChallengeResult(
    Guid ChallengeId,
    DateTime ExpiresAt);
