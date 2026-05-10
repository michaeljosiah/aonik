namespace Aonik.SharedKernel.Abstractions.Ai.Speech;

/// <summary>
/// Thrown when the caller asks to mutate a speech provider or recipe in a way that conflicts
/// with referential integrity (e.g. deleting a provider that an active recipe references).
/// Endpoint exception filter maps this to <c>409 Conflict</c> with the carried usage payload
/// so the admin UI can render "blocked by these recipes — click to edit" links inline.
/// </summary>
public sealed class SpeechLibraryUsageBlockedException : Exception
{
    public SpeechProviderUsage Usage { get; }

    public SpeechLibraryUsageBlockedException(string message, SpeechProviderUsage usage)
        : base(message)
    {
        Usage = usage;
    }
}

/// <summary>
/// Thrown when a request's payload is structurally invalid for a reason the EF model can't
/// detect — e.g. a TTS provider with an STT-shaped <c>Config</c>, a chained recipe whose
/// <c>SttProviderId</c> resolves to a TTS provider, or a tenant trying to write an id with the
/// reserved <see cref="SpeechLibraryConstants.BuiltInIdPrefix"/> prefix. Maps to
/// <c>422 Unprocessable Entity</c>.
/// </summary>
public sealed class SpeechLibraryValidationException : Exception
{
    public string? FieldName { get; }

    public SpeechLibraryValidationException(string message, string? fieldName = null)
        : base(message)
    {
        FieldName = fieldName;
    }
}

/// <summary>
/// Thrown when a built-in archetype id is supplied to an operation that only accepts tenant-owned
/// rows (e.g. <c>UpdateAsync</c> on a <c>built-in:</c> id). The remediation is to clone the
/// built-in first, then mutate the clone. Maps to <c>409 Conflict</c>.
/// </summary>
public sealed class SpeechLibraryImmutableBuiltInException : Exception
{
    public string BuiltInId { get; }

    public SpeechLibraryImmutableBuiltInException(string builtInId)
        : base($"Built-in archetype '{builtInId}' is immutable. Clone it first to get an editable copy.")
    {
        BuiltInId = builtInId;
    }
}
