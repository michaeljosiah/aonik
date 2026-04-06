using FastEndpoints;
using Microsoft.AspNetCore.Http;

using Aonik.Platform.Contracts.Api.Identity;
using Aonik.Platform.Contracts.Models.Authentication;
using Aonik.Platform.Contracts.Services.Identity;

namespace Aonik.Platform.Endpoints.Identity;

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
        Summary(s =>
        {
            s.Summary = "Exchange credentials for auth token";
            s.Description = "Authenticates a user via grant type (password, authorization code, or refresh token) and returns an access token.";
            s.Response(200, "Token issued successfully");
            s.Response(400, "Invalid credentials or grant");
        });
        Options(x => x.WithTags("Identity"));
    }

    public override async Task HandleAsync(TokenRequestDto req, CancellationToken ct)
    {
        try
        {
            var request = new TokenRequest(
                req.GrantType,
                req.ClientId,
                req.Username,
                req.Password,
                req.Scope,
                req.RedirectUri,
                req.CodeVerifier,
                req.AuthorizationCode,
                req.RefreshToken);

            var response = await _identityService.TokenAsync(request, ct);

            await Send.OkAsync(new TokenResponseDto(
                response.AccessToken,
                response.RefreshToken,
                response.ExpiresIn,
                response.TokenType,
                response.IdToken), ct);
        }
        catch (InvalidOperationException ex)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new { error = ex.Message }, ct);
        }
    }
}
