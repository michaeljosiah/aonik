using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Health;

internal class HealthEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/health");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Check API health";
            s.Description = "Returns a simple health check response with current status and timestamp.";
            s.Response(200, "API is healthy");
        });
        Options(x => x.WithTags("Health"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync(new { Status = "Healthy", Timestamp = DateTime.UtcNow }, ct);
    }
}
