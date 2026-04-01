using Aonik.Platform.Contracts.Api.Identity;
using Aonik.Platform.Contracts.Models.Notifications;
using Aonik.Platform.Contracts.Services.Notifications;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Identity;

public class RegisterNotificationDeviceEndpoint : Endpoint<RegisterNotificationDeviceRequestDto, RegisterNotificationDeviceResponseDto>
{
    private readonly INotificationDeviceService _notificationDeviceService;

    public RegisterNotificationDeviceEndpoint(INotificationDeviceService notificationDeviceService)
    {
        _notificationDeviceService = notificationDeviceService;
    }

    public override void Configure()
    {
        Post("/profiles/customers/me/notification-devices");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(RegisterNotificationDeviceRequestDto req, CancellationToken ct)
    {
        try
        {
            var response = await _notificationDeviceService.RegisterCurrentUserDeviceAsync(
                new RegisterNotificationDeviceRequest(req.Provider, req.Platform, req.DeviceToken),
                ct);

            await Send.OkAsync(
                new RegisterNotificationDeviceResponseDto(
                    response.NotificationDeviceId,
                    response.Provider,
                    response.Platform,
                    response.LastSeenAtUtc),
                ct);
        }
        catch (ArgumentException exception)
        {
            AddError(exception.Message);
            await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);
        }
    }
}
