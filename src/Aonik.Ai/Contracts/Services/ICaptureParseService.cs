using Aonik.Ai.Contracts.Models;

namespace Aonik.Ai.Contracts.Services;

/// <summary>
/// Parses a captured image / text / audio-transcript into a structured draft
/// proposal (Spec 047). The operation is read-shaped: it persists nothing to
/// the record (no PaymentLog, no other entity) — only a subsequent
/// user-confirmed create (Spec 045) writes one. Every parse is recorded as an
/// <c>AiRun</c> with <c>UseCase=capture_parse</c>; the raw payload is never stored.
/// </summary>
public interface ICaptureParseService
{
    Task<CaptureParseResponse> ParseAsync(
        CaptureParseRequest request,
        CancellationToken cancellationToken = default);
}
