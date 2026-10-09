using System.Text.Json;

using Aonik.SharedKernel.Abstractions.Tasks;

namespace Aonik.Commerce.Services.GiftCards;

internal sealed record GiftCardDeliveryTaskPayload(Guid DeliveryId, int Sequence);

internal sealed class GiftCardDeliveryTaskHandler(IGiftCardDeliveryService deliveries) : ITaskActionHandler
{
    public string ActionType => GiftCardDeliveryService.ActionType;

    public async Task<TaskActionResult> ExecuteAsync(TaskActionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<GiftCardDeliveryTaskPayload>(context.ActionPayloadJson);
            if (payload is null) return new(TaskActionOutcome.Failed, Error: "The gift card task payload is invalid.");
            return await deliveries.SendAsync(context, payload.DeliveryId, payload.Sequence, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return new(TaskActionOutcome.Failed, Error: "Gift card delivery could not complete. Retry the same task."); }
    }
}
