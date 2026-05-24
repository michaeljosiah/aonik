using Aonik.Platform.Contracts.Models.Messaging;
using Aonik.Platform.Contracts.Services.Messaging;
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
    private readonly IEmailSender _emailSender;
    private readonly ISmsSender _smsSender;

    public MessagingHealthEndpoint(IEmailSender emailSender, ISmsSender smsSender)
    {
        _emailSender = emailSender;
        _smsSender = smsSender;
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

    public override Task HandleAsync(CancellationToken ct)
    {
        var response = new MessagingHealthResponse(
            Email: new MessagingChannelHealth(
                Configured: _emailSender.IsConfigured,
                Provider: _emailSender.ProviderName,
                Reason: _emailSender.UnconfiguredReason),
            Sms: new MessagingChannelHealth(
                Configured: _smsSender.IsConfigured,
                Provider: _smsSender.ProviderName,
                Reason: _smsSender.UnconfiguredReason));

        return Send.OkAsync(response, ct);
    }
}
