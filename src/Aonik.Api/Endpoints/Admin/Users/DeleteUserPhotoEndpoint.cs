using FastEndpoints;

using Aonik.Platform.Contracts.Services.Identity;
using Aonik.Platform.Contracts.Api.Identity;

namespace Aonik.Api.Endpoints.Admin.Users;

public class DeleteUserPhotoEndpoint : EndpointWithoutRequest<CustomerPhotoDeleteResponse>
{
    private readonly IAccessManagementService _accessManagementService;

    public DeleteUserPhotoEndpoint(IAccessManagementService accessManagementService)
    {
        _accessManagementService = accessManagementService;
    }

    public override void Configure()
    {
        Delete("/admin/users/{userId}/photo");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = Route<Guid>("userId");

        var result = await _accessManagementService.DeleteUserPhotoAsync(userId, ct);
        if (result == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(new CustomerPhotoDeleteResponse(result.Status), ct);
    }
}
