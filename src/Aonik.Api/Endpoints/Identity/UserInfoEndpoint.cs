using FastEndpoints;

using Aonik.Api.Contracts.Identity;
using Aonik.Application.Services.Identity;

namespace Aonik.Api.Endpoints.Identity;

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
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await _identityService.GetUserInfoAsync(ct);
        await Send.OkAsync(new UserInfoResponseDto(
            result.UserId,
            result.Email,
            result.FirstName,
            result.LastName,
            result.Roles,
            result.TenantId,
            result.PartyId), ct);
    }
}
