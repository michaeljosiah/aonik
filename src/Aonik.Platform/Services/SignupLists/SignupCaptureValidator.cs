using Aonik.Platform.Contracts.Models.SignupLists;
using Aonik.SharedKernel.Validation;
using FluentValidation;

namespace Aonik.Platform.Services.SignupLists;

internal sealed class SignupCaptureValidator : AbstractValidator<SignupCaptureRequest>
{
    public static SignupCaptureRequest Normalize(SignupCaptureRequest request) => request with
    {
        Email = request.Email?.Trim() ?? string.Empty,
        Postcode = NormalizePostcode(request.Postcode),
        Name = TrimOptional(request.Name),
        Phone = TrimOptional(request.Phone),
        Country = TrimOptional(request.Country)?.ToUpperInvariant(),
        Service = TrimOptional(request.Service)
    };

    public SignupCaptureValidator(string listType)
    {
        RuleFor(x => x.Email).Email();
        RuleFor(x => x.ConsentVersion).RequiredText(32);
        RuleFor(x => x).Must(x => new[] { x.Email, x.Name, x.Phone, x.Country, x.Service, x.ConsentVersion }
            .All(value => value is null || !value.Any(char.IsControl)))
            .WithMessage("Fields must not contain control characters.");

        if (listType == SignupListTypes.DeliveryAvailability)
            RuleFor(x => x.Postcode).NotEmpty().MaximumLength(16)
                .Matches(@"^(?:[A-Z]{1,2}[0-9][A-Z0-9]?|GIR) [0-9][A-Z]{2}$")
                .WithMessage("A full UK postcode is required.");
        else
            RuleFor(x => x.Postcode).Empty();

        if (listType == SignupListTypes.PrivateTable)
        {
            RuleFor(x => x.Name!).RequiredText(200);
            RuleFor(x => x.Phone).OptionalText(32)
                .Matches(@"^\+?[0-9 ().-]+$")
                .Must(phone => phone is null || phone.Count(char.IsAsciiDigit) is >= 6 and <= 17)
                .When(x => x.Phone is not null)
                .WithMessage("Enter a telephone number, or leave this blank.");
            RuleFor(x => x.Country!).CountryCode();
            RuleFor(x => x.Service!).RequiredText(64);
        }
        else
        {
            RuleFor(x => x.Name).Empty();
            RuleFor(x => x.Phone).Empty();
            RuleFor(x => x.Country).Empty();
            RuleFor(x => x.Service).Empty();
        }
    }

    private static string? TrimOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizePostcode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (value.Length > 16) return value; // Keep oversized input invalid; do not truncate personal data.
        var compact = string.Concat(value.Where(character => !char.IsWhiteSpace(character))).ToUpperInvariant();
        return compact.Length > 3 ? compact.Insert(compact.Length - 3, " ") : compact;
    }
}
