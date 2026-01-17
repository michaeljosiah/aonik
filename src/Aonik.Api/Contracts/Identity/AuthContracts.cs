namespace Aonik.Api.Contracts.Identity;

public record TokenRequestDto(
    string GrantType,
    string ClientId,
    string? Username,
    string? Password,
    string? Scope,
    string? RedirectUri,
    string? CodeVerifier,
    string? AuthorizationCode);

public record TokenResponseDto(
    string AccessToken,
    string? RefreshToken,
    int ExpiresIn,
    string TokenType,
    string? IdToken);

public record UserInfoResponseDto(
    Guid UserId,
    string Email,
    string? FirstName,
    string? LastName,
    IReadOnlyCollection<string> Roles,
    Guid TenantId,
    Guid PartyId);

public record ForgotPasswordRequestDto(
    string Email,
    Guid TenantId);

public record ForgotPasswordResponseDto(string Status);
