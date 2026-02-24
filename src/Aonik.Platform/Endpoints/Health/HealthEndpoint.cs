using FastEndpoints;

namespace Aonik.Platform.Endpoints.Health;

internal class HealthEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/health");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync(new { Status = "Healthy", Timestamp = DateTime.UtcNow }, ct);
    }
}
