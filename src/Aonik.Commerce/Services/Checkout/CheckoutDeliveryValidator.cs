using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Services.Catalog;
using Aonik.SharedKernel.Validation;

using FluentValidation;

namespace Aonik.Commerce.Services.Checkout;

internal sealed class CheckoutDeliveryValidator : AbstractValidator<CheckoutDeliveryDetails>
{
    public CheckoutDeliveryValidator()
    {
        RuleFor(x => x.Purchaser.Email).Email();
        RuleFor(x => x.Purchaser.FirstName).RequiredText(100);
        RuleFor(x => x.Purchaser.LastName).RequiredText(100);
        Phone(RuleFor(x => x.Purchaser.Phone));
        RuleFor(x => x.Address.Line1).RequiredText(200);
        RuleFor(x => x.Address.Line2).OptionalText(200);
        RuleFor(x => x.Address.City).RequiredText(100);
        RuleFor(x => x.Address.Region).OptionalText(100);
        RuleFor(x => x.Address.Postcode).RequiredText(32);
        RuleFor(x => x.Address.CountryCode).CountryCode();
        RuleFor(x => x.DeliveryDate).NotEqual(default(DateOnly));
        When(x => x.Recipient is not null, () =>
        {
            RuleFor(x => x.Recipient!.Name).RequiredText(201);
            Phone(RuleFor(x => x.Recipient!.Phone));
        });
        RuleFor(x => x.Notes).OptionalText(1000)
            .Must(value => value is null || !value.Any(c => char.IsControl(c) && c is not ('\r' or '\n')))
            .WithMessage("Delivery notes must not contain control characters other than line breaks.");
        RuleFor(x => x.WindowId).Empty().WithMessage("Delivery time windows are not currently offered.");
        RuleFor(x => x).Must(x => new[]
        {
            x.Purchaser.Email, x.Purchaser.FirstName, x.Purchaser.LastName, x.Purchaser.Phone,
            x.Address.Line1, x.Address.Line2, x.Address.City, x.Address.Region, x.Address.Postcode,
            x.Address.CountryCode, x.Recipient?.Name, x.Recipient?.Phone
        }.All(value => value is null || !value.Any(char.IsControl)))
            .WithMessage("Contact and address fields must not contain control characters.");
    }

    public static CheckoutDeliveryDetails NormalizeAndValidate(CheckoutDeliveryDetails details)
    {
        var purchaser = details.Purchaser ?? new CheckoutContactDto("", "", "", "");
        var address = details.Address ?? new DeliveryAddressDto("", null, "", null, "", "");
        var normalized = details with
        {
            Purchaser = new CheckoutContactDto(Trim(purchaser.Email), Trim(purchaser.FirstName),
                Trim(purchaser.LastName), Trim(purchaser.Phone)),
            Address = new DeliveryAddressDto(Trim(address.Line1), Optional(address.Line2), Trim(address.City),
                Optional(address.Region), Trim(address.Postcode).ToUpperInvariant(), Trim(address.CountryCode).ToUpperInvariant()),
            Recipient = details.Recipient is { } recipient ? new DeliveryRecipientDto(Trim(recipient.Name), Trim(recipient.Phone)) : null,
            Notes = Optional(details.Notes),
            WindowId = Optional(details.WindowId)
        };
        var validation = new CheckoutDeliveryValidator().Validate(normalized);
        if (!validation.IsValid)
            throw new StorefrontValidationException(string.Join(" ", validation.Errors
                .Select(error => $"{error.PropertyName}: {error.ErrorMessage}").Distinct()));
        return normalized;
    }

    private static void Phone(IRuleBuilder<CheckoutDeliveryDetails, string> rule) => rule
        .RequiredText(32)
        .Matches(@"^\+?[0-9 ().-]+$")
        .Must(value => value is not null && value.Count(char.IsAsciiDigit) is >= 6 and <= 17)
        .WithMessage("Enter a telephone number containing 6 to 17 digits.");

    private static string Trim(string? value) => value?.Trim() ?? string.Empty;
    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
