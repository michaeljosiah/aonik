using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Agents.Endpoints.TenantExtensions;

/// <summary>
/// Base for tenant-extension transition endpoints that carry a request body (review / enable-scripts).
/// Maps the service's outcome uniformly: missing row → 404, illegal transition
/// (<see cref="InvalidOperationException"/>) → 409, success → 200 with the updated DTO.
/// </summary>
public abstract class TenantExtensionTransitionEndpoint<TRequest, TResponse> : Endpoint<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : class
{
    protected async Task TransitionAsync(Func<Task<TResponse?>> action, CancellationToken ct)
    {
        try
        {
            var result = await action();
            if (result is null)
            {
                await Send.NotFoundAsync(ct);
                return;
            }
            await Send.OkAsync(result, ct);
        }
        catch (InvalidOperationException ex)
        {
            AddError(ex.Message);
            await Send.ErrorsAsync(StatusCodes.Status409Conflict, ct);
        }
    }
}

/// <summary>
/// Request-less variant for route-only transitions (submit / activate / deactivate). A POST with no
/// body must NOT bind a request DTO, or FastEndpoints returns 415 for the bodyless call — so these
/// read the id via <c>Route&lt;Guid&gt;("Id")</c>, matching the codebase's CancelTaskEndpoint pattern.
/// </summary>
public abstract class TenantExtensionTransitionEndpoint<TResponse> : EndpointWithoutRequest<TResponse>
    where TResponse : class
{
    protected async Task TransitionAsync(Func<Task<TResponse?>> action, CancellationToken ct)
    {
        try
        {
            var result = await action();
            if (result is null)
            {
                await Send.NotFoundAsync(ct);
                return;
            }
            await Send.OkAsync(result, ct);
        }
        catch (InvalidOperationException ex)
        {
            AddError(ex.Message);
            await Send.ErrorsAsync(StatusCodes.Status409Conflict, ct);
        }
    }
}
