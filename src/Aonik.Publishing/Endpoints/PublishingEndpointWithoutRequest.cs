using FastEndpoints;
using Microsoft.AspNetCore.Http;

using Aonik.Publishing.Services;

namespace Aonik.Publishing.Endpoints;

internal abstract class PublishingEndpointWithoutRequest<TResponse> : EndpointWithoutRequest<TResponse>
{
    protected async Task GuardedAsync(Func<Task> action)
    {
        try { await action(); }
        catch (PublishingException problem)
        {
            await Send.ResultAsync(Results.Problem(statusCode: problem.Status, title: problem.Code, detail: problem.Message,
                extensions: new Dictionary<string, object?> { ["code"] = problem.Code }));
        }
    }
}
