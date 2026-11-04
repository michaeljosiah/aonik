namespace Aonik.Publishing.Endpoints;

public sealed record SubmissionPath(Guid Id);
public sealed record UploadPath(Guid Id, Guid UploadId);
[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed class SubmitPackageModel
{
    public Guid RevisionId { get; set; }
    public Guid OperationId { get; set; }
}
