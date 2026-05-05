namespace Aonik.SharedKernel.Validation;

/// <summary>
/// Marks an endpoint request DTO as deliberately exempt from the
/// "every request DTO must have a Validator" architecture rule.
/// </summary>
/// <remarks>
/// Opt-out is the exception, not the default. Apply this attribute only
/// when the DTO is structurally incapable of carrying invalid input —
/// e.g. an empty marker record with no parameters, or a request whose
/// inputs are bound entirely from a trusted server-side source. Always
/// include a justification in the constructor argument so reviewers
/// understand why no validator exists.
///
/// The architecture test in <c>Aonik.Api.Tests</c> reads this attribute
/// and excludes annotated DTOs from the validator-coverage check.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class NoValidationAttribute : Attribute
{
    /// <summary>Reason the DTO is exempt; surfaced to reviewers and the arch test failure message.</summary>
    public string Justification { get; }

    public NoValidationAttribute(string justification)
    {
        if (string.IsNullOrWhiteSpace(justification))
        {
            throw new ArgumentException("A justification is required for opt-out.", nameof(justification));
        }

        Justification = justification;
    }
}
