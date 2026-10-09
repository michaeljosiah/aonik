namespace Aonik.Platform.Contracts.Models.ContactEnquiries;

public record ContactImageUpload(string FileName, string ContentType, byte[] Content);
public record ProcessedContactImage(byte[] Content, string ContentType, string FileName);
public record ContactEnquirySubmission(Guid SubmissionId, string Name, string Email, string Topic,
    string? OrderNumber, string Message, IReadOnlyList<ContactImageUpload> Images);
public record ContactEnquiryReceipt(Guid Id, DateTime ReceivedAtUtc);
public record ContactImageProblem(int Index, string FileName, string Code, string Message);
public record ContactEnquirySummaryDto(Guid Id, string Name, string Email, string Topic, DateTime ReceivedAtUtc, int ImageCount);
public record ContactEnquiryAttachmentDto(Guid Id, string FileName, string ContentType, long SizeBytes);
public record ContactEnquiryDetailDto(Guid Id, string Name, string Email, string Topic, string? OrderNumber,
    string Message, DateTime ReceivedAtUtc, IReadOnlyList<ContactEnquiryAttachmentDto> Images);
public record ContactEnquiryImageDownload(Stream Content, string ContentType, string FileName);
public record ContactEnquiriesConfiguration(bool IsEnabled, IReadOnlyDictionary<string, string> TopicRecipients, string AdminOrigin);

public static class ContactEnquiryTopics
{
    public static bool IsKnown(string? topic) => topic is "order" or "new" or "dish" or "delivery" or "gift" or "other";
}
