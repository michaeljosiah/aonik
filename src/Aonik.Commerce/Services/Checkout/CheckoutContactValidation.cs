using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Services.Catalog;
using Aonik.SharedKernel.Validation;
using FluentValidation;

namespace Aonik.Commerce.Services.Checkout;

internal static class CheckoutContactValidation
{
    public static CheckoutContactDto ValidateGiftPurchaser(CheckoutContactDto contact)
    {
        contact = CartDraftData.Normalize(new CartCheckoutDraftDto(Purchaser: contact)).Purchaser
            ?? throw new StorefrontValidationException("Enter your order-confirmation email address.");
        if (contact.Email.Length > 254 || !System.Net.Mail.MailAddress.TryCreate(contact.Email, out var email) || email.Address != contact.Email)
            throw new StorefrontValidationException("Enter your order-confirmation email address.");
        return contact;
    }

    public static CheckoutContactDto ValidateContact(CheckoutContactDto contact)
    {
        var normalized = CartDraftData.Normalize(new CartCheckoutDraftDto(Purchaser: contact)).Purchaser
            ?? throw new StorefrontValidationException("Purchaser contact details are required.");
        var result = ContactValidator().Validate(normalized);
        if (!result.IsValid) throw new StorefrontValidationException(string.Join(" ", result.Errors.Select(x => x.ErrorMessage)));
        return normalized;
    }

    public static InlineValidator<CheckoutContactDto> ContactValidator()
    {
        var validator = new InlineValidator<CheckoutContactDto>();
        validator.RuleFor(x => x.Email).Email();
        validator.RuleFor(x => x.FirstName).RequiredText(100);
        validator.RuleFor(x => x.LastName).RequiredText(100);
        Phone(validator.RuleFor(x => x.Phone));
        return validator;
    }

    public static void ValidateAddress(DeliveryAddressDto address, string? phone)
    {
        var result = AddressValidator().Validate(address);
        if (!result.IsValid) throw new StorefrontValidationException(string.Join(" ", result.Errors.Select(x => x.ErrorMessage)));
        var phoneValidator = new InlineValidator<DeliveryRecipientDto>();
        Phone(phoneValidator.RuleFor(x => x.Phone));
        if (!phoneValidator.Validate(new DeliveryRecipientDto("", phone ?? "")).IsValid)
            throw new StorefrontValidationException("Enter a recipient phone number for the courier.");
    }

    public static InlineValidator<DeliveryAddressDto> AddressValidator()
    {
        var validator = new InlineValidator<DeliveryAddressDto>();
        validator.RuleFor(x => x.Line1).RequiredText(200);
        validator.RuleFor(x => x.Line2).OptionalText(200);
        validator.RuleFor(x => x.City).RequiredText(100);
        validator.RuleFor(x => x.Region).OptionalText(100);
        validator.RuleFor(x => x.Postcode).RequiredText(32);
        validator.RuleFor(x => x.CountryCode).CountryCode();
        return validator;
    }

    public static void Phone<T>(IRuleBuilder<T, string> rule) => rule.RequiredText(32)
        .Matches(@"^\+?[0-9 ().-]+$").Must(value => value != null && value.Count(char.IsAsciiDigit) is >= 6 and <= 17)
        .WithMessage("Enter a telephone number containing 6 to 17 digits.");
}
