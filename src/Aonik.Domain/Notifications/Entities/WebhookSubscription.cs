using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Notifications.Entities;

public class WebhookSubscription : AuditableEntity, ITenantScoped
{
    public Guid WebhookSubscriptionId { get; private set; }
    public Guid TenantId { get; private set; }
    public string SubscriberName { get; private set; } = string.Empty;
    public string EventTypesJson { get; private set; } = string.Empty;
    public string EndpointUrl { get; private set; } = string.Empty;
    public string? SecretRef { get; private set; }
    public bool IsActive { get; private set; }

    private WebhookSubscription() { }

    public WebhookSubscription(Guid tenantId, string subscriberName, string eventTypesJson, string endpointUrl)
    {
        WebhookSubscriptionId = Id;
        TenantId = tenantId;
        SubscriberName = subscriberName;
        EventTypesJson = eventTypesJson;
        EndpointUrl = endpointUrl;
        IsActive = true;
    }

    public void UpdateSubscriberName(string subscriberName)
    {
        SubscriberName = subscriberName;
    }

    public void UpdateEventTypes(string eventTypesJson)
    {
        EventTypesJson = eventTypesJson;
    }

    public void UpdateEndpointUrl(string endpointUrl)
    {
        EndpointUrl = endpointUrl;
    }

    public void UpdateSecretRef(string secretRef)
    {
        SecretRef = secretRef;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
