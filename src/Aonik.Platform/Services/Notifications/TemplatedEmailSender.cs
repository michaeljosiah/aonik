using Aonik.Platform.Contracts.Models.Notifications;
using Aonik.Platform.Contracts.Services.Messaging;
using Aonik.Platform.Contracts.Services.Notifications;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.SharedKernel.Abstractions.Messaging;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Platform.Services.Notifications;

internal sealed class TemplatedEmailSender(
    INotificationTemplateService templates,
    IEmailSender sender,
    IPublicBusinessProfileService businessProfile,
    ITenantProvider tenantProvider) : ITemplatedEmailSender
{
    public async Task SendAsync(TemplatedEmailMessage message, CancellationToken cancellationToken = default)
    {
        _ = tenantProvider.GetCurrentTenantId();
        var profile = await businessProfile.GetCurrentAsync(cancellationToken);
        var model = new Dictionary<string, object?>(message.Model)
        {
            // Published tenant facts are authoritative; a caller cannot supply another brand.
            ["brand"] = new Dictionary<string, object?>
            {
                ["display_name"] = profile?.DisplayName ?? "AONIK",
                ["logo_url"] = profile?.LogoUrl,
                ["website"] = profile?.Website,
                ["contact_email"] = profile?.Contact?.Email,
                ["contact_phone"] = profile?.Contact?.Phone
            }
        };
        var rendered = await templates.RenderAsync(
            new RenderNotificationTemplateRequest(message.TemplateName, "Email", model), cancellationToken);

        // Failure must reach the durable event dispatcher so its existing retry policy applies.
        await sender.SendAsync(new EmailMessage(message.To, rendered.Subject, rendered.Body), cancellationToken);
    }
}
