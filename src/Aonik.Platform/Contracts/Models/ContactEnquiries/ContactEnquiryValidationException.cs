namespace Aonik.Platform.Contracts.Models.ContactEnquiries;

public sealed class ContactEnquiryValidationException(
    IReadOnlyDictionary<string, string[]> fieldErrors, IReadOnlyList<ContactImageProblem>? imageProblems = null)
    : Exception("Check the enquiry fields and images before submitting.")
{
    public IReadOnlyDictionary<string, string[]> FieldErrors { get; } = fieldErrors;
    public IReadOnlyList<ContactImageProblem> ImageProblems { get; } = imageProblems ?? [];
}
