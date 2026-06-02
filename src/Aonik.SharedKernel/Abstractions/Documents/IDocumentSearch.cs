namespace Aonik.SharedKernel.Abstractions.Documents;

/// <summary>
/// Scoped semantic search over indexed documents, for agents and search surfaces.
/// The <see cref="DocumentSearchScope"/> is mandatory — there is deliberately no
/// overload that omits it — so a prompt cannot widen its own retrieval scope.
/// Tenant isolation is applied beneath the scope by the vector store (fail-closed);
/// this contract adds owner-party / classification / purpose constraints on top.
/// See <a href="../../../docs/specifications/033.extract-documents-module.html">Spec 033 §14</a>.
/// </summary>
public interface IDocumentSearch
{
    /// <summary>
    /// Returns the top matching document chunks for <paramref name="query"/>, restricted to
    /// <paramref name="scope"/>. Callers must build the scope from authenticated context.
    /// </summary>
    Task<IReadOnlyList<DocumentChunkHit>> SearchAsync(
        string query,
        DocumentSearchScope scope,
        int topK = 8,
        CancellationToken cancellationToken = default);
}
