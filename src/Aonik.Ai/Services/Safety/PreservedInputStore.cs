namespace Aonik.Ai.Services.Safety;

/// <summary>
/// Moves a blocked <em>input</em> into protected storage so §12 preservation has something to
/// preserve, without the prompt itself ever becoming a "reference".
///
/// <para>
/// This seam exists because of a genuine tension. An input block's only artefact is the child's own
/// words: writing them into <c>SafetyArtefact.Reference</c> breaks that entity's contract and §11's
/// position that a child's input is not material we keep — but at a
/// <see cref="Aonik.SharedKernel.Abstractions.Safety.SafetyCategories.Reportable"/> category the
/// obligation runs the other way and preservation is not discretionary. The resolution is not to keep
/// the text inline but to put it somewhere access-controlled and keep only the key.
/// </para>
///
/// <para>
/// <strong>Not registered by default</strong>, because no protected store is configured in this
/// solution. That absence is not silently tolerated: the gate logs at critical and the escalation
/// records <c>MaterialPreserved = false</c>, so the responsible person is told they are acting on a
/// record with nothing behind it rather than being left to assume otherwise.
/// </para>
/// </summary>
public interface IPreservedInputStore
{
    /// <summary>
    /// Store the input under access control and return the key. Throwing is a legitimate outcome and
    /// is recorded as a preservation failure — never swallowed into a success.
    /// </summary>
    Task<string> PreserveAsync(
        Guid subjectPartyId,
        string input,
        CancellationToken cancellationToken = default);
}
