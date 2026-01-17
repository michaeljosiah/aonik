namespace Aonik.Application.Abstractions.Authentication;

public record IdpPasswordValidationResult(bool IsValid, string? ErrorMessage);
