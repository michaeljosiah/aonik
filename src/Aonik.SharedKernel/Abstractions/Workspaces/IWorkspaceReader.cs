namespace Aonik.SharedKernel.Abstractions.Workspaces;

/// <summary>
/// Read-only projection for other modules (Spec 089 §11).
///
/// <para>
/// Deliberately narrow: does this party own this workspace, and how large is it. A module that needs more than
/// that is probably reaching for something the workspace layer should not expose — <strong>this contract carries
/// no way to read file contents</strong>, which is what keeps
/// <a href="../../../../docs/decisions/016-workspaces-as-platform-primitives.md">ADR-016</a>'s seam intact and is
/// the reason Spec 096 can say the storage layer's ignorance of content is preserved.
/// </para>
/// </summary>
public interface IWorkspaceReader
{
    Task<bool> IsOwnedByAsync(
        Guid workspaceId, Guid partyId, CancellationToken cancellationToken = default);

    /// <summary>Bytes currently held. <c>long</c>, because a 3GB workspace is ordinary here.</summary>
    Task<long> GetTotalBytesAsync(Guid workspaceId, CancellationToken cancellationToken = default);

    Task<int> CountForOwnerAsync(Guid ownerPartyId, CancellationToken cancellationToken = default);
}
