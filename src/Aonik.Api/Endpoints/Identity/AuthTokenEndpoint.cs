using FastEndpoints;

using Aonik.Api.Contracts.Identity;
using Aonik.Platform.Contracts.Models.Authentication;
using Aonik.Platform.Contracts.Services.Identity;

namespace Aonik.Api.Endpoints.Identity;

public class AuthTokenEndpoint : Endpoint<TokenRequestDto, TokenResponseDto>
{
    private readonly IIdentityService _identityService;

    public AuthTokenEndpoint(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public override void Configure()
    {
        Post("/auth/token");
        AllowAnonymous();
    }

    public override async Task HandleAsync(TokenRequestDto req, CancellationToken ct)
    {
        var request = new TokenRequest(
            req.GrantType,
            req.ClientId,
            req.Username,
            req.Password,
            req.Scope,
            req.RedirectUri,
            req.CodeVerifier,
            req.AuthorizationCode);

        var response = await _identityService.TokenAsync(request, ct);

        await Send.OkAsync(new TokenResponseDto(
            response.AccessToken,
            response.RefreshToken,
            response.ExpiresIn,
            response.TokenType,
            response.IdToken), ct);
    }
}
