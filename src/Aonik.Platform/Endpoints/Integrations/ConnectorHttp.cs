using Microsoft.AspNetCore.Http;

using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Platform.Endpoints.Integrations;

internal static class ConnectorHttp
{
    public static async Task<IResult> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        try { T result = await operation(); return result is null ? Results.NotFound() : Results.Ok(result); }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (ConnectorAccessException exception) { return Results.Json(new { code = exception.Code, error = exception.Message }, statusCode: exception.Code switch { "Conflict" => 409, "RateLimited" => 429, _ => 403 }); }
        catch (ConnectorProviderException exception) { return Results.Json(new { code = exception.Code }, statusCode: exception.Transient ? 503 : 422); }
        catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
        catch (System.Text.Json.JsonException) { return Results.BadRequest(new { error = "The action parameters do not match the typed contract." }); }
    }
}
