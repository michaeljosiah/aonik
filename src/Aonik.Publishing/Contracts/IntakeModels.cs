namespace Aonik.Publishing.Contracts;

public sealed record UploadReceipt(Guid Id, Guid RevisionId, string Status, int PartSizeBytes, int TotalParts,
    long ReceivedBytes, DateTime ExpiresAt, IReadOnlyList<int> MissingParts);
public sealed record SubmitPackageRequest(Guid RevisionId, Guid OperationId);
public sealed record ValidationPreview(Guid ReportId, string Status, string Code, string PackageSha256,
    long PackageBytes, string ReaderCommit, string VerifierImage, DateTime? CompletedAt, PackageVerificationResult? Package);
public sealed record PackageAsset(string Id, string Path, string MediaType, long ByteLength, string Sha256);
public sealed record PackageVerificationResult(bool Passed, string Code, string ReaderCommit, string ArchiveSha256,
    long ArchiveBytes, string WorldId, string Edition, string Fingerprint, string Kind, string? BaseFingerprint,
    int? PartNumber, string? CoverAssetId, IReadOnlyList<PackageAsset> Assets, VerifiedPackagePresentation? Presentation = null);
public sealed record VerifiedPackagePresentation(int SchemaVersion, string ManifestSha256, System.Text.Json.JsonElement Manifest,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] System.Text.Json.JsonElement? Details = null);
public sealed record VerifiedInput(string Path, string Sha256, long Bytes);

/// <summary>Private, session-owned immutable storage. There is deliberately no public URL method.</summary>
public interface IPublishingObjectStore
{
    string LocationId { get; }
    Task StorePartAsync(Guid tenantId, Guid sessionId, int number, string sha256, long bytes, Stream content, CancellationToken cancellationToken = default);
    Task<VerifiedInput> AssembleAsync(Guid tenantId, Guid sessionId, string sha256, long bytes,
        IReadOnlyList<(int Number, string Sha256)> parts, CancellationToken cancellationToken = default);
    Task<VerifiedInput> OpenPackageAsync(Guid tenantId, Guid sessionId, string sha256, long bytes, CancellationToken cancellationToken = default);
    Task DeleteSessionAsync(Guid tenantId, Guid sessionId, CancellationToken cancellationToken = default);
}
public interface IPackageVerifier
{
    Task<PackageVerificationResult> VerifyAsync(VerifiedInput input, Services.PackageVerifierOptions policy, CancellationToken cancellationToken = default);
}
public interface IPublicationIntakeService
{
    Task<UploadReceipt> BeginAsync(Guid submissionId, CancellationToken cancellationToken = default);
    Task<UploadReceipt> GetAsync(Guid submissionId, Guid sessionId, CancellationToken cancellationToken = default);
    Task<UploadReceipt> PutPartAsync(Guid submissionId, Guid sessionId, int number, string sha256, long bytes, Stream content, CancellationToken cancellationToken = default);
    Task<UploadReceipt> CompleteAsync(Guid submissionId, Guid sessionId, CancellationToken cancellationToken = default);
    Task CancelAsync(Guid submissionId, Guid sessionId, CancellationToken cancellationToken = default);
    Task<ValidationPreview> SubmitAsync(Guid submissionId, SubmitPackageRequest request, CancellationToken cancellationToken = default);
    Task<ValidationPreview?> PreviewAsync(Guid submissionId, CancellationToken cancellationToken = default);
    Task<(Stream Content, string FileName)> DownloadAsync(Guid submissionId, CancellationToken cancellationToken = default);
}
