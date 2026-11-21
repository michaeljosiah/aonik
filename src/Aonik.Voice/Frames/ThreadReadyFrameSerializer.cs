using System.Text.Json;
using Voxa.Frames;

namespace Aonik.Voice.Frames;

/// <summary>
/// AONIK-only frame serializer wired into <c>WebSocketAudioSink</c> via its
/// <c>customSerializer</c> hook for thread readiness and approval review links.
/// Voxa handles the standard envelopes.
///
/// <para>
/// Replaces the previous <c>AonikVoiceWebSocketSink</c> subclass — Voxa's sink is sealed but
/// accepts this hook in its constructor, so AONIK no longer needs its own copy of the send
/// discipline (semaphore, binary-vs-text dispatch, close handling).
/// </para>
/// </summary>
public static class ThreadReadyFrameSerializer
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Serializes AONIK frames and leaves other frames to the sink.
    /// </summary>
    public static string? Serialize(Frame frame)
    {
        if (frame is ApprovalReviewFrame review)
        {
            string path = $"/approvals?requestId={review.ApprovalRequestId:D}";
            string? url = Uri.TryCreate(review.AdminUiBaseUrl, UriKind.Absolute, out var origin)
                && origin.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(origin.UserInfo)
                ? new Uri(origin, path).AbsoluteUri : null;
            return JsonSerializer.Serialize(new { type = "approvalReview", reviewPath = path, reviewUrl = url }, JsonOpts);
        }

        if (frame is not ThreadReadyFrame ready) return null;

        return JsonSerializer.Serialize(new
        {
            type = "threadReady",
            chatThreadId = ready.ChatThreadId,
            isNew = ready.IsNew,
        }, JsonOpts);
    }
}
