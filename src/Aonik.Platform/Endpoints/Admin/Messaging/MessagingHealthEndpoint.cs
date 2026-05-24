using Aonik.Platform.Contracts.Models.Messaging;
using Aonik.Platform.Contracts.Models.Settings;
using Aonik.Platform.Contracts.Services.Settings;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Messaging;

/// <summary>
/// Admin-only read endpoint. Returns the configuration health of the
/// outbound messaging channels (email + SMS) so the Admin UI can warn
/// the operator before they trigger flows that rely on delivery —
/// most importantly the user-invite flow, where the placeholder gets
/// created locally but the recipient never sees the email if the
/// provider isn't wired up.
/// </summary>
internal sealed class MessagingHealthEndpoint : EndpointWithoutRequest<MessagingHealthResponse>
{
    private const string AzureCommunicationServices = "AzureCommunicationServices";

    private readonly ICommunicationProviderSettingsService _settingsService;

    public MessagingHealthEndpoint(ICommunicationProviderSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public override void Configure()
    {
        Get("/admin/messaging/health");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Messaging configuration health";
            s.Description =
                "Reports whether the email and SMS providers have the configuration they need to "
                + "dispatch messages. Used by the Admin UI to warn operators up-front before they "
                + "trigger flows that depend on outbound communication (e.g. user invitations).";
            s.Response(200, "Health snapshot");
            s.Response(401, "Not authenticated");
            s.Response(403, "Caller lacks AdminPolicy");
        });
        Options(x => x.WithTags("Messaging"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var settings = await _settingsService.GetAsync(ct);
        var response = new MessagingHealthResponse(
            Email: BuildEmailHealth(settings.Email),
            Sms: BuildSmsHealth(settings.Sms));

        await Send.OkAsync(response, ct);
    }

    private static MessagingChannelHealth BuildEmailHealth(EmailChannelSettingsSnapshot settings)
    {
        if (!string.Equals(settings.ActiveProvider, AzureCommunicationServices, StringComparison.OrdinalIgnoreCase))
        {
            return new MessagingChannelHealth(
                Configured: false,
                Provider: settings.ActiveProvider,
                Reason: $"Email provider '{settings.ActiveProvider}' is selected, but no email connector is registered for it.");
        }

        var azure = settings.AzureCommunicationServices;
        if (azure?.HasConnectionString != true)
        {
            return new MessagingChannelHealth(
                Configured: false,
                Provider: settings.ActiveProvider,
                Reason: "Communication.Email.AzureCommunicationServices.ConnectionString is missing.");
        }

        if (string.IsNullOrWhiteSpace(azure.FromAddress))
        {
            return new MessagingChannelHealth(
                Configured: false,
                Provider: settings.ActiveProvider,
                Reason: "Communication.Email.AzureCommunicationServices.FromAddress is missing.");
        }

        return new MessagingChannelHealth(Configured: true, Provider: settings.ActiveProvider, Reason: null);
    }

    private static MessagingChannelHealth BuildSmsHealth(SmsChannelSettingsSnapshot settings)
    {
        if (!string.Equals(settings.ActiveProvider, AzureCommunicationServices, StringComparison.OrdinalIgnoreCase))
        {
            return new MessagingChannelHealth(
                Configured: false,
                Provider: settings.ActiveProvider,
                Reason: $"SMS provider '{settings.ActiveProvider}' is selected, but no SMS connector is registered for it.");
        }

        var azure = settings.AzureCommunicationServices;
        if (azure?.HasConnectionString != true)
        {
            return new MessagingChannelHealth(
                Configured: false,
                Provider: settings.ActiveProvider,
                Reason: "Communication.Sms.AzureCommunicationServices.ConnectionString is missing.");
        }

        if (string.IsNullOrWhiteSpace(azure.FromPhoneNumber))
        {
            return new MessagingChannelHealth(
                Configured: false,
                Provider: settings.ActiveProvider,
                Reason: "Communication.Sms.AzureCommunicationServices.FromPhoneNumber is missing.");
        }

        return new MessagingChannelHealth(Configured: true, Provider: settings.ActiveProvider, Reason: null);
    }
}
