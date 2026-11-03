using Aonik.Agents.Contracts.Models;
using FastEndpoints;

namespace Aonik.Agents.Endpoints.Rooms;

internal abstract class ChatRoomWithoutRequestEndpoint<TResponse> : EndpointWithoutRequest<TResponse>
{
    protected async Task ExecuteAsync(Func<Task<TResponse>> operation, CancellationToken ct)
    {
        try { await Send.OkAsync(await operation(), ct); }
        catch (ChatRoomException exception)
        {
            AddError(exception.Message);
            await Send.ErrorsAsync(exception.StatusCode, ct);
        }
    }
    protected static async Task<RoomActionResponse> DoneAsync(Func<Task> operation)
    {
        await operation();
        return new RoomActionResponse(true);
    }
}
