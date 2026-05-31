using System.Net;
using System.Net.Http.Json;

using Aonik.Infrastructure.Persistence;
using Aonik.Platform.Contracts.Api.Identity;
using Aonik.Platform.Contracts.Models.Notifications;
using Aonik.Platform.Contracts.Services.Notifications;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Api.Tests;

public class NotificationDeviceEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public NotificationDeviceEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RegisterNotificationDevice_ShouldPersistDeviceForCurrentUser()
    {
        var auth = TestAuthOptions.Create().WithRoles("PersonalUser");
        var client = await _factory.CreateAuthenticatedClientAsync(auth);

        var response = await client.PostAsJsonAsync(
            "/profiles/customers/me/notification-devices",
            new RegisterNotificationDeviceRequestDto("fcm", "android", "token-123"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = auth.TenantId!.Value;
        var dbContext = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        var device = await dbContext.NotificationDevices.SingleAsync(x =>
            x.TenantId == auth.TenantId!.Value
            && x.UserId == auth.UserId
            && x.DeviceToken == "token-123");
        device.TenantId.Should().Be(auth.TenantId!.Value);
        device.UserId.Should().Be(auth.UserId);
        device.Provider.Should().Be("fcm");
        device.Platform.Should().Be("android");
        device.DeviceToken.Should().Be("token-123");
    }

    [Fact]
    public async Task RegisterNotificationDevice_ShouldUpsertExistingToken()
    {
        var auth = TestAuthOptions.Create().WithRoles("PersonalUser");
        var client = await _factory.CreateAuthenticatedClientAsync(auth);

        var firstResponse = await client.PostAsJsonAsync(
            "/profiles/customers/me/notification-devices",
            new RegisterNotificationDeviceRequestDto("fcm", "android", "token-123"));
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondResponse = await client.PostAsJsonAsync(
            "/profiles/customers/me/notification-devices",
            new RegisterNotificationDeviceRequestDto("fcm", "android", "token-123"));
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = auth.TenantId!.Value;
        var dbContext = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        var count = await dbContext.NotificationDevices.CountAsync(x =>
            x.TenantId == auth.TenantId!.Value
            && x.UserId == auth.UserId
            && x.DeviceToken == "token-123");
        count.Should().Be(1);
    }

    [Fact]
    public async Task RegisterNotificationDevice_ShouldReturnValidationError_WhenTokenMissing()
    {
        var auth = TestAuthOptions.Create().WithRoles("PersonalUser");
        var client = await _factory.CreateAuthenticatedClientAsync(auth);

        var response = await client.PostAsJsonAsync(
            "/profiles/customers/me/notification-devices",
            new RegisterNotificationDeviceRequestDto("fcm", "android", string.Empty));

        // Empty DeviceToken is rejected by the Validator<T> attached to the
        // request DTO, surfaced as 422 Unprocessable Content per the global
        // FastEndpoints ErrorOptions.StatusCode convention.
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        await using var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = auth.TenantId!.Value;
        var dbContext = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        var count = await dbContext.NotificationDevices.CountAsync(x =>
            x.TenantId == auth.TenantId!.Value
            && x.DeviceToken == string.Empty);
        count.Should().Be(0);
    }

    [Fact]
    public async Task CreateNotification_ShouldDispatchPushToRegisteredDevice()
    {
        var auth = TestAuthOptions.Create().WithRoles("PersonalUser");
        var client = await _factory.CreateAuthenticatedClientAsync(auth);
        var pushSender = _factory.GetPushNotificationSender();
        pushSender.Reset();

        var registerResponse = await client.PostAsJsonAsync(
            "/profiles/customers/me/notification-devices",
            new RegisterNotificationDeviceRequestDto("fcm", "android", "token-abc"));
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = auth.TenantId!.Value;
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        await notificationService.CreateForUserAsync(
            new CreateNotificationRequest(
                auth.TenantId!.Value,
                auth.UserId,
                "TestNotification",
                "Tests",
                "Hello",
                "World",
                "Info",
                "/notifications",
                null,
                null));

        pushSender.Requests.Should().ContainSingle();
        var request = pushSender.Requests.Single();
        request.UserId.Should().Be(auth.UserId);
        request.ActionUrl.Should().Be("/notifications");
        request.Targets.Should().ContainSingle(x => x.DeviceToken == "token-abc");
    }

    [Fact]
    public async Task CreateNotification_ShouldDeactivateInvalidDevice_WhenPushProviderRejectsToken()
    {
        var auth = TestAuthOptions.Create().WithRoles("PersonalUser");
        var client = await _factory.CreateAuthenticatedClientAsync(auth);

        var registerResponse = await client.PostAsJsonAsync(
            "/profiles/customers/me/notification-devices",
            new RegisterNotificationDeviceRequestDto("fcm", "android", "token-invalid"));
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var registered = await registerResponse.Content.ReadFromJsonAsync<RegisterNotificationDeviceResponseDto>();
        registered.Should().NotBeNull();

        var pushSender = _factory.GetPushNotificationSender();
        pushSender.Reset();
        pushSender.SetInvalidDeviceIds(registered!.NotificationDeviceId);

        await using var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = auth.TenantId!.Value;
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        await notificationService.CreateForUserAsync(
            new CreateNotificationRequest(
                auth.TenantId!.Value,
                auth.UserId,
                "TestNotification",
                "Tests",
                "Hello",
                "World",
                "Info",
                "/notifications",
                null,
                null));

        var dbContext = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        var device = await dbContext.NotificationDevices.SingleAsync(x => x.Id == registered.NotificationDeviceId);
        device.IsActive.Should().BeFalse();
        device.InvalidatedAtUtc.Should().NotBeNull();
        device.LastError.Should().Be("FCM token rejected by provider.");
    }
}
