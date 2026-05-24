using Aonik.Platform.Contracts.Api.Settings;
using Aonik.Platform.Contracts.Services.Messaging;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Settings;

/// <summary>
/// Posts a one-off test message via the currently active email or SMS
/// provider so an operator can verify their configuration without
/// running a full invite/registration flow. Mirrors a "send test"
/// button on the Admin UI's SettingsCommunicationPage.
///
/// Behaviour: surfaces both the typed
/// <see cref="MessagingNotConfiguredException"/> ("no provider wired
/// up") and any provider-side delivery failure (auth errors, rate
/// limits, etc.) as a structured 200 response with <c>Sent=false</c>
/// and a human-readable <c>ErrorMessage</c>. We deliberately do NOT
/// return 4xx/5xx for delivery failures because the operator just
/// wants to see what happened — surfacing them in the response body
/// keeps the UI flow simple.
/// </summary>
internal class SendCommunicationTestEndpoint
    : Endpoint<SendCommunicationTestRequest, SendCommunicationTestResponse>
{
    private readonly IEmailSender _emailSender;
    private readonly ISmsSender _smsSender;

    public SendCommunicationTestEndpoint(IEmailSender emailSender, ISmsSender smsSender)
    {
        _emailSender = emailSender;
        _smsSender = smsSender;
    }

    public override void Configure()
    {
        Post("/admin/settings/communication-provider/test-send");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Send a test email or SMS";
            s.Description =
                "Fires a one-off message via the active provider so the operator can confirm "
                + "outbound communication works. Returns 200 with Sent=false + ErrorMessage when "
                + "delivery fails (rather than a non-200) so the UI can render the failure inline.";
            s.Response(200, "Result of the test send");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Settings"));
    }

    public override async Task HandleAsync(SendCommunicationTestRequest req, CancellationToken ct)
    {
        var channel = req.Channel?.Trim() ?? string.Empty;
        var recipient = req.Recipient?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(channel) || string.IsNullOrWhiteSpace(recipient))
        {
            AddError("Channel and Recipient are required.");
            await Send.ErrorsAsync(400, ct);
            return;
        }

        if (string.Equals(channel, "Email", StringComparison.OrdinalIgnoreCase))
        {
            await TrySendEmailAsync(recipient, req.Subject, req.Body, ct);
            return;
        }

        if (string.Equals(channel, "SMS", StringComparison.OrdinalIgnoreCase))
        {
            await TrySendSmsAsync(recipient, req.Body, ct);
            return;
        }

        AddError($"Unsupported channel '{channel}'. Expected 'Email' or 'SMS'.");
        await Send.ErrorsAsync(400, ct);
    }

    private async Task TrySendEmailAsync(string recipient, string? subject, string? body, CancellationToken ct)
    {
        var resolvedSubject = string.IsNullOrWhiteSpace(subject) ? "Aonik test email" : subject!;
        var resolvedBody = string.IsNullOrWhiteSpace(body)
            ? "This is a test email from the Aonik Admin UI to verify that the configured email "
              + "provider can deliver messages. If you received this, outbound email is working."
            : body!;

        try
        {
            await _emailSender.SendAsync(
                new EmailMessage(recipient, resolvedSubject, resolvedBody),
                ct);
            await Send.OkAsync(new SendCommunicationTestResponse(
                Sent: true,
                Channel: "Email",
                Provider: _emailSender.ProviderName,
                ErrorMessage: null), ct);
        }
        catch (MessagingNotConfiguredException ex)
        {
            await Send.OkAsync(new SendCommunicationTestResponse(
                Sent: false,
                Channel: "Email",
                Provider: _emailSender.ProviderName,
                ErrorMessage: ex.Reason), ct);
        }
        catch (Exception ex)
        {
            await Send.OkAsync(new SendCommunicationTestResponse(
                Sent: false,
                Channel: "Email",
                Provider: _emailSender.ProviderName,
                ErrorMessage: ex.Message), ct);
        }
    }

    private async Task TrySendSmsAsync(string recipient, string? body, CancellationToken ct)
    {
        var resolvedBody = string.IsNullOrWhiteSpace(body)
            ? "Aonik test SMS — outbound SMS is configured correctly."
            : body!;

        try
        {
            await _smsSender.SendAsync(new SmsMessage(recipient, resolvedBody), ct);
            await Send.OkAsync(new SendCommunicationTestResponse(
                Sent: true,
                Channel: "SMS",
                Provider: _smsSender.ProviderName,
                ErrorMessage: null), ct);
        }
        catch (MessagingNotConfiguredException ex)
        {
            await Send.OkAsync(new SendCommunicationTestResponse(
                Sent: false,
                Channel: "SMS",
                Provider: _smsSender.ProviderName,
                ErrorMessage: ex.Reason), ct);
        }
        catch (Exception ex)
        {
            await Send.OkAsync(new SendCommunicationTestResponse(
                Sent: false,
                Channel: "SMS",
                Provider: _smsSender.ProviderName,
                ErrorMessage: ex.Message), ct);
        }
    }
}
