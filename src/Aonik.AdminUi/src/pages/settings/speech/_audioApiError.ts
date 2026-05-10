/**
 * Shared error-message extractor for the speech library's audio test endpoints. Three call
 * sites share this:
 *   - <c>ProviderTestSection</c> (Test panel inside the provider edit sheet)
 *   - <c>ChatSpeechTab</c> (Preview button on the chat speech tab — Phase E)
 *   - <c>RecipeTestPanel</c> (Test button on recipe cards — Phase E)
 *
 * <para>
 * Three error envelope shapes show up in practice:
 *   1. The TTS endpoint uses <c>responseType: 'blob'</c>, so even error responses arrive as a
 *      Blob. We read the blob, parse it as JSON, then look for FastEndpoints' standard
 *      <c>{ message, errors: { generalErrors } }</c> envelope.
 *   2. JSON error envelope (the STT endpoint, the global Aonik exception handler, and most
 *      others) — pluck the same fields from <c>error.response.data</c> directly.
 *   3. Network failure / non-axios error — fall back to <c>error.message</c> then to the
 *      provided default.
 * </para>
 *
 * <para>
 * The Aonik global exception handler (see <c>ExceptionHandlerConfiguration.cs</c>) returns
 * <c>{ error, code, ... }</c> envelopes for <c>SpeechLibrary*</c> exceptions. We surface
 * <c>error</c> when present so 409/422 messages reach the user verbatim.
 * </para>
 */

interface FastEndpointsErrorEnvelope {
  message?: string;
  errors?: { generalErrors?: string[] } & Record<string, string[] | undefined>;
}

interface AonikExceptionEnvelope {
  error?: string;
}

export async function extractAudioApiError(
  err: unknown,
  fallback: string,
): Promise<string> {
  if (err && typeof err === "object") {
    const response = (err as { response?: { data?: unknown } }).response;
    const data = response?.data;
    if (data instanceof Blob) {
      try {
        const text = await data.text();
        const parsed = JSON.parse(text) as FastEndpointsErrorEnvelope &
          AonikExceptionEnvelope;
        const fromEnvelope = pickFromEnvelope(parsed);
        if (fromEnvelope) return fromEnvelope;
      } catch {
        // not JSON; fall through to the generic axios message
      }
    } else if (data && typeof data === "object") {
      const fromEnvelope = pickFromEnvelope(
        data as FastEndpointsErrorEnvelope & AonikExceptionEnvelope,
      );
      if (fromEnvelope) return fromEnvelope;
    }
    const message = (err as { message?: string }).message;
    if (message) return message;
  }
  return fallback;
}

function pickFromEnvelope(
  envelope: FastEndpointsErrorEnvelope & AonikExceptionEnvelope,
): string | null {
  // Aonik global exception handler ({ error, code, ... }) wins — it carries the curated
  // SpeechLibrary*Exception messages we actually want users to see.
  if (envelope.error && envelope.error.length > 0) return envelope.error;

  // FastEndpoints' AddError(...) collects under errors.generalErrors; prefer those over the
  // generic "One or more errors occurred!" message that always rides along.
  const general = envelope.errors?.generalErrors;
  if (general && general.length > 0) return general.join("; ");

  // Fall through to other field-level errors if any.
  if (envelope.errors) {
    for (const [key, value] of Object.entries(envelope.errors)) {
      if (key === "generalErrors") continue;
      if (Array.isArray(value) && value.length > 0)
        return `${key}: ${value.join("; ")}`;
    }
  }

  if (envelope.message && envelope.message !== "One or more errors occurred!") {
    return envelope.message;
  }
  return null;
}
