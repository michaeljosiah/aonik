using Aonik.Platform.Contracts.Models.Identity;
using Aonik.SharedKernel.Validation;

using FluentValidation;

namespace Aonik.Platform.Services.Identity;

internal sealed class CustomerAddressInputValidator : AbstractValidator<CustomerAddressWrite>
{
    public CustomerAddressInputValidator()
    {
        RuleFor(x => x.Type).RequiredText(32);
        RuleFor(x => x.Line1).RequiredText(256);
        RuleFor(x => x.Line2).MaximumLength(256);
        RuleFor(x => x.Line3).MaximumLength(256);
        RuleFor(x => x.City).RequiredText(128);
        RuleFor(x => x.State).MaximumLength(128);
        RuleFor(x => x.Postcode).RequiredText(32);
        RuleFor(x => x.Country).CountryCode();
        RuleFor(x => x).Must(x => !HasControls(x))
            .WithMessage("Address fields must not contain control characters.");
    }

    public static CustomerAddressWrite Normalize(CustomerAddressWrite value)
    {
        // Keep unsupported controls visible to the shared validator, including at field edges.
        if (HasControls(value)) return value;
        return value with
        {
            Type = value.Type?.Trim() ?? "", Line1 = value.Line1?.Trim() ?? "",
            Line2 = Optional(value.Line2), Line3 = Optional(value.Line3), City = value.City?.Trim() ?? "",
            State = Optional(value.State), Postcode = value.Postcode?.Trim() ?? "",
            Country = value.Country?.Trim().ToUpperInvariant() ?? ""
        };
    }

    private static bool HasControls(CustomerAddressWrite value)
        => new[] { value.Type, value.Line1, value.Line2, value.Line3, value.City, value.State, value.Postcode, value.Country }
            .Any(field => field is not null && field.Any(char.IsControl));

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
