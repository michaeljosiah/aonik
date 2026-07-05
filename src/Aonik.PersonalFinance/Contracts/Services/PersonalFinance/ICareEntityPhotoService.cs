using Aonik.PersonalFinance.Contracts.Models;

namespace Aonik.PersonalFinance.Contracts.Services;

/// <summary>
/// Sets and clears the single banner image of a <c>CareEntity</c> (Spec 049 §7–§9).
/// Composes the Documents module (Spec 035) server-side: an uploaded image becomes an
/// owner-scoped <c>Document</c>, the entity's <c>PhotoDocumentId</c> is pointed at it, and a
/// replaced/removed banner is erased. Every operation is isolated to the current tenant + user;
/// a non-owned id reads as not-found (the service returns null / false — the endpoint sends 404).
/// </summary>
public interface ICareEntityPhotoService
{
    /// <summary>
    /// Uploads (or replaces) the entity's banner image and returns the updated entity with a
    /// resolved, signed <c>PhotoUrl</c>. Returns <c>null</c> when the entity is not owned by the
    /// current user. Throws <see cref="ArgumentException"/> when the image fails validation
    /// (content type / size, Spec 049 §10) — no document or blob is written in that case.
    /// </summary>
    Task<CareEntityResponse?> SetPhotoAsync(
        Guid careEntityId,
        Stream image,
        string fileName,
        string contentType,
        long lengthBytes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the entity's banner: removes <c>PhotoDocumentId</c> and erases the document.
    /// Returns <c>false</c> when the entity is not owned by the current user; idempotent when no
    /// photo is set.
    /// </summary>
    Task<bool> RemovePhotoAsync(
        Guid careEntityId,
        CancellationToken cancellationToken = default);
}
