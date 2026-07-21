namespace Aonik.Commerce.Services.Catalog;

/// <summary>
/// A selection or authoring write that breaks one of the Spec 066 §9 rules. Carries the rule id so
/// endpoints can surface a precise message and Spec 068's cart writes can relay the same failure.
/// Derives from <see cref="InvalidOperationException"/> so existing catalog error handling still
/// treats it as a client fault.
/// </summary>
public sealed class OptionValidationException : InvalidOperationException
{
    public OptionValidationException(string ruleId, string message)
        : base($"{ruleId}: {message}")
    {
        RuleId = ruleId;
    }

    /// <summary>The Spec 066 §9 rule that rejected the write, e.g. "V2".</summary>
    public string RuleId { get; }
}
