using FastEndpoints;

using Aonik.Platform.Contracts.Services.Identity;
using Aonik.Platform.Contracts.Api.Identity;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Users;

internal class DeleteUserPhotoEndpoint : EndpointWithoutRequest<CustomerPhotoDeleteResponse>
{
    private readonly IAccessManagementService _accessManagementService;

    public DeleteUserPhotoEndpoint(IAccessManagementService accessManagementService)
    {
        _accessManagementService = accessManagementService;
    }

    public override void Configure()
    {
        Delete("/admin/users/{userId}/photo");
        Policies("AdminWritePolicy");
        Summary(s =>
        {
            s.Summary = "Delete user profile photo";
            s.Description = "Removes the profile photo for the specified user, reverting to the default avatar.";
            s.Response(200, "Photo deleted");
            s.Response(401, "Not authenticated");
            s.Response(404, "User not found");
        });
        Options(x => x.WithTags("User Administration"));
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
