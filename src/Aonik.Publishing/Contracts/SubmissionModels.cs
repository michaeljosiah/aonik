using System.Text.Json.Serialization;

namespace Aonik.Publishing.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateSubmissionRequest(
    Guid ClientRequestId,
    string ChannelKey,
    string Title,
    string CreatorDisplayName,
    string Summary,
    string Language,
    string WorldId,
    string Edition,
    string PackageFileName,
    long DeclaredPackageBytes,
    string DeclaredPackageSha256,
    string RightsDeclarationVersion,
    bool DistributionPermissionGranted,
    PublicationEditorialMaterial? EditorialMaterial = null);

public sealed record SubmissionRevisionResponse(
    Guid Id,
    int Number,
    string RequestDigest,
    int RequestDigestVersion,
    string Title,
    string CreatorDisplayName,
    string Summary,
    string Language,
    string WorldId,
    string Edition,
    string PackageFileName,
    long DeclaredPackageBytes,
    string DeclaredPackageSha256,
    string RightsDeclarationVersion,
    bool DistributionPermissionGranted,
    bool PackageVerified,
    PublicationEditorialMaterial? EditorialMaterial = null);

public sealed record SubmissionReceipt(
    Guid Id,
    Guid ClientRequestId,
    string ChannelKey,
    string Status,
    DateTime CreatedAt,
    SubmissionRevisionResponse Revision,
    string Version = "");

public sealed record CreateSubmissionResult(bool Inserted, SubmissionReceipt Submission);

public sealed record SubmissionPage(int Page, int PageSize, IReadOnlyList<SubmissionReceipt> Items);

public interface ICreatorSubmissionService
{
    Task<CreateSubmissionResult> CreateAsync(CreateSubmissionRequest request, CancellationToken cancellationToken = default);
    Task<SubmissionReceipt?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SubmissionPage> ListAsync(int page = 1, int pageSize = 25, CancellationToken cancellationToken = default);
}
