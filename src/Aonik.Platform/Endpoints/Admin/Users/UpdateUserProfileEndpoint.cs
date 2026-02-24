using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;

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
