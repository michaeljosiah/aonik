using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Aonik.Voice.Endpoints;

/// <summary>
/// Endpoint route extensions for the voice WebSocket. Mirrors the pattern used
/// by <c>MapAdminNotificationStreaming</c> in <c>Aonik.Platform</c>.
///
/// <para>
/// The caller is responsible for invoking <c>app.UseWebSockets()</c> earlier
/// in the pipeline AND for chaining <c>.RequireAuthorization("MobileVoicePolicy")</c>
/// (and any CORS policy) on the returned builder. Mirroring the AdminNotification
/// pattern keeps the auth/cors composition at the composition root where it's
/// reviewable.
/// </para>
/// </summary>
public static class VoiceWebSocketEndpointExtensions
{
    /// <summary>
    /// Maps <c>WSS /ai/voice</c>. Authorization and CORS are caller's responsibility
    /// (chain <c>.RequireAuthorization("MobileVoicePolicy")</c> at the call site).
    /// </summary>
    public static IEndpointConventionBuilder MapAonikVoiceEndpoints(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/ai/voice")
    {
        return endpoints.MapGet(pattern, VoiceWebSocketEndpoint.HandleAsync)
            .WithName("AonikVoiceWebSocket")
            .WithTags("Voice")
            .WithSummary("Real-time voice mode WebSocket")
            .WithDescription("Streams microphone PCM and JSON envelopes between Payabo mobile and the AONIK voice pipeline. See docs/specifications/022.aonik-voice-realtime.md.");
    }
}
