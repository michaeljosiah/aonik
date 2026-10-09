namespace Aonik.Platform.Contracts.Models.ContactEnquiries;

public sealed class ContactImageValidationException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
