using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Users;

internal class UpdateUserProfileEndpoint : Endpoint<UpdateUserProfileRequest>
{
    private readonly IAccessManagementService _accessManagementService;

    public UpdateUserProfileEndpoint(IAccessManagementService accessManagementService)
    {
        _accessManagementService = accessManagementService;
    }

    public override void Configure()
    {
        Put("/admin/users/{userId}/profile");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Update user profile";
            s.Description = "Updates the profile information for the specified user such as name and contact details.";
            s.Response(204, "Profile updated");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
            s.Response(404, "User not found");
        });
        Options(x => x.WithTags("User Administration"));
    }

    public override async Task HandleAsync(UpdateUserProfileRequest request, CancellationToken ct)
    {
        var userId = Route<Guid>("userId");

        try
        {
            await _accessManagementService.UpdateUserProfileAsync(userId, request, ct);
            await Send.NoContentAsync(ct);
        }
        catch (InvalidOperationException ex)
        {
            AddError(ex.Message);
            await Send.ErrorsAsync(400, ct);
        }
    }
}
