namespace Aonik.Platform.Contracts.Models.ContactEnquiries;

public sealed class ContactEnquiryConflictException()
    : Exception("This submission reference has already been used for different enquiry details.");
