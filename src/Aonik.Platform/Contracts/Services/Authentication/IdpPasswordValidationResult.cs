namespace Aonik.Platform.Contracts.Services.Authentication;

public record IdpPasswordValidationResult(bool IsValid, string? ErrorMessage);
