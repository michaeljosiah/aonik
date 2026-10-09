using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Services.Catalog;
using Aonik.SharedKernel.Validation;

using FluentValidation;

namespace Aonik.Commerce.Services.Checkout;

internal sealed class CheckoutDeliveryValidator : AbstractValidator<CheckoutDeliveryDetails>
{
    public CheckoutDeliveryValidator()
    {
        RuleFor(x => x.Purchaser).SetValidator(CheckoutContactValidation.ContactValidator());
        RuleFor(x => x.Address).SetValidator(CheckoutContactValidation.AddressValidator());
        RuleFor(x => x.DeliveryDate).NotEqual(default(DateOnly));
        When(x => x.Recipient is not null, () =>
        {
            RuleFor(x => x.Recipient!.Name).RequiredText(201);
            CheckoutContactValidation.Phone(RuleFor(x => x.Recipient!.Phone));
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

    private static string Trim(string? value) => value?.Trim() ?? string.Empty;
    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
