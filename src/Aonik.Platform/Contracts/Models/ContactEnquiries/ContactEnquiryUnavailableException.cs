namespace Aonik.Platform.Contracts.Models.ContactEnquiries;

public sealed class ContactEnquiryUnavailableException(string message = "Enquiries are temporarily unavailable. Please try again later.")
    : Exception(message);
