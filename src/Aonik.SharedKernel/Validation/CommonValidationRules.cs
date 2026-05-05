using FluentValidation;

namespace Aonik.SharedKernel.Validation;

/// <summary>
/// Reusable FluentValidation rule extensions for common AONIK domain
/// invariants. Endpoint validators should prefer these over re-implementing
/// the same checks so the rules stay consistent across the platform.
/// </summary>
public static class CommonValidationRules
{
    /// <summary>
    /// Requires a non-empty Guid (rejects <see cref="Guid.Empty"/>).
    /// </summary>
    public static IRuleBuilderOptions<T, Guid> RequiredId<T>(this IRuleBuilder<T, Guid> ruleBuilder)
        => ruleBuilder.NotEmpty().WithMessage("Must be a non-empty GUID.");

    /// <summary>
    /// For nullable Guid properties: when supplied, must be non-empty.
    /// </summary>
    public static IRuleBuilderOptions<T, Guid?> ValidIdWhenSupplied<T>(this IRuleBuilder<T, Guid?> ruleBuilder)
        => ruleBuilder
            .Must(value => !value.HasValue || value.Value != Guid.Empty)
            .WithMessage("Must be a non-empty GUID when supplied.");

    /// <summary>
    /// Requires an ISO-4217 currency code: exactly 3 uppercase A-Z letters.
    /// </summary>
    public static IRuleBuilderOptions<T, string> CurrencyCode<T>(this IRuleBuilder<T, string> ruleBuilder)
        => ruleBuilder
            .NotEmpty().WithMessage("Currency code is required.")
            .Length(3).WithMessage("Currency code must be 3 characters (ISO-4217).")
            .Matches("^[A-Z]{3}$").WithMessage("Currency code must be 3 uppercase letters (e.g. USD, GBP, NGN).");

    /// <summary>
    /// Requires an ISO-3166-1 alpha-2 country code: exactly 2 uppercase A-Z letters.
    /// </summary>
    public static IRuleBuilderOptions<T, string> CountryCode<T>(this IRuleBuilder<T, string> ruleBuilder)
        => ruleBuilder
            .NotEmpty().WithMessage("Country code is required.")
            .Length(2).WithMessage("Country code must be 2 characters (ISO-3166-1 alpha-2).")
            .Matches("^[A-Z]{2}$").WithMessage("Country code must be 2 uppercase letters (e.g. US, GB, NG).");

    /// <summary>
    /// Loose email validation — non-empty, contains '@' and '.' after the at,
    /// max 254 chars (RFC 5321). Use this for human-entered email fields.
    /// </summary>
    public static IRuleBuilderOptions<T, string> EmailAddress<T>(this IRuleBuilder<T, string> ruleBuilder)
        => ruleBuilder
            .NotEmpty().WithMessage("Email is required.")
            .MaximumLength(254).WithMessage("Email may not exceed 254 characters.")
            .EmailAddress().WithMessage("Email is not in a valid format.");

    /// <summary>
    /// Phone numbers stored in E.164 form: leading '+' followed by 8-15 digits.
    /// </summary>
    public static IRuleBuilderOptions<T, string> PhoneE164<T>(this IRuleBuilder<T, string> ruleBuilder)
        => ruleBuilder
            .NotEmpty().WithMessage("Phone number is required.")
            .Matches(@"^\+[1-9]\d{7,14}$").WithMessage("Phone must be in E.164 format (e.g. +447700900123).");

    /// <summary>
    /// Page-size guard for paged-list endpoints. Defaults: 1-100 inclusive.
    /// </summary>
    public static IRuleBuilderOptions<T, int> PageSize<T>(this IRuleBuilder<T, int> ruleBuilder, int min = 1, int max = 100)
        => ruleBuilder
            .InclusiveBetween(min, max)
            .WithMessage($"Page size must be between {min} and {max}.");

    /// <summary>
    /// Page-number guard for paged-list endpoints (1-based).
    /// </summary>
    public static IRuleBuilderOptions<T, int> PageNumber<T>(this IRuleBuilder<T, int> ruleBuilder)
        => ruleBuilder
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be 1 or greater.");

    /// <summary>
    /// Money amount: must be positive and within typical financial precision.
    /// </summary>
    public static IRuleBuilderOptions<T, decimal> PositiveMoney<T>(this IRuleBuilder<T, decimal> ruleBuilder)
        => ruleBuilder
            .GreaterThan(0).WithMessage("Amount must be greater than zero.")
            .LessThanOrEqualTo(1_000_000_000m).WithMessage("Amount exceeds maximum supported value.");

    /// <summary>
    /// Money amount that may be zero or positive (e.g. discounts, totals).
    /// </summary>
    public static IRuleBuilderOptions<T, decimal> NonNegativeMoney<T>(this IRuleBuilder<T, decimal> ruleBuilder)
        => ruleBuilder
            .GreaterThanOrEqualTo(0).WithMessage("Amount cannot be negative.")
            .LessThanOrEqualTo(1_000_000_000m).WithMessage("Amount exceeds maximum supported value.");

    /// <summary>
    /// Bounded text field — non-empty plus length cap. Use for human-entered
    /// short strings like names, descriptions, identifiers.
    /// </summary>
    public static IRuleBuilderOptions<T, string> RequiredText<T>(this IRuleBuilder<T, string> ruleBuilder, int maxLength = 256)
        => ruleBuilder
            .NotEmpty().WithMessage("Value is required.")
            .MaximumLength(maxLength).WithMessage($"Value may not exceed {maxLength} characters.");

    /// <summary>
    /// Optional text — when supplied, must respect the length cap. Empty/null is allowed.
    /// </summary>
    public static IRuleBuilderOptions<T, string?> OptionalText<T>(this IRuleBuilder<T, string?> ruleBuilder, int maxLength = 1024)
        => ruleBuilder
            .MaximumLength(maxLength).WithMessage($"Value may not exceed {maxLength} characters.");
}
