using FastEndpoints;

using Aonik.Platform.Contracts.Api.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Identity;

public class UserInfoEndpoint : EndpointWithoutRequest<UserInfoResponseDto>
{
    private readonly IIdentityService _identityService;

    public UserInfoEndpoint(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public override void Configure()
    {
        Get("/identity/userinfo");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "Get authenticated user info";
            s.Description = "Returns identity details for the authenticated user including roles, tenant, party, and photo URLs.";
            s.Response(200, "User info returned");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Identity"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        try
        {
            var result = await _identityService.GetUserInfoAsync(ct);
            await Send.OkAsync(new UserInfoResponseDto(
                result.UserId,
                result.Email,
                result.FirstName,
                result.LastName,
                result.Roles,
                result.TenantId,
                result.PartyId,
                result.PhotoUrl,
                result.PhotoUrlSmall,
                result.PhotoUrlTiny), ct);
        }
        catch (OperationCanceledException)
        {
            // Client disconnected/canceled request.
        }
    }
}
