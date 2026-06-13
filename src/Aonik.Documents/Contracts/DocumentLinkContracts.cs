namespace Aonik.Documents.Contracts;

/// <summary>A document's link to a Simi target (Spec 046).</summary>
public sealed record DocumentLinkDto(
    Guid Id,
    Guid DocumentId,
    string TargetType,
    Guid TargetId,
    DateTime CreatedAt);

/// <summary>Request to attach a document to a CareEntity / PaymentLog / commitment.</summary>
public sealed record AddDocumentLinkRequest(
    string TargetType,
    Guid TargetId);
