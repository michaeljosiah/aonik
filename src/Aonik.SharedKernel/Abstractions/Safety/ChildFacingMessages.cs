namespace Aonik.SharedKernel.Abstractions.Safety;

/// <summary>
/// What the child actually reads (Spec 096 §10.1, §10.2, §18.7).
///
/// <para>
/// <strong>A seven-year-old told they triggered a "violence filter" learns they did something
/// wrong.</strong> They did not — most of what is blocked at these thresholds is a knight fighting a
/// dragon, which is the most common request a six-year-old makes. So there is no category name, no
/// warning styling, no red, and nothing that reads as an accusation.
/// </para>
///
/// <para>
/// It lives in SharedKernel rather than the product UI because a client that renders its own copy of
/// this will eventually render a technical one — the first time somebody debugging wants to know
/// <em>why</em>, and ships it.
/// </para>
/// </summary>
public static class ChildFacingMessages
{
    /// <summary>
    /// The message for an outcome. <strong>A block and a failed check read identically</strong>, which
    /// is deliberate twice over: a failure to check is not a child's problem to understand, and a
    /// distinguishable outage message is a probe — try until the wording changes, then send the thing
    /// you wanted through.
    /// </summary>
    public static ChildFacingMessage For(SafetyDecisionOutcome outcome, string safetyBand)
        => outcome switch
        {
            SafetyDecisionOutcome.Allowed => new ChildFacingMessage(string.Empty, CanRetry: false),

            // Held is not a refusal, and saying nothing would make an adult reviewing look like the
            // product being broken. §8 requires the child be told, in age-appropriate terms.
            SafetyDecisionOutcome.HeldForReview => IsYoung(safetyBand)
                ? new ChildFacingMessage(
                    "A grown-up is having a look at this one first. It will be here soon.", CanRetry: false)
                : new ChildFacingMessage(
                    "This one is waiting for your parent or guardian to look at it.", CanRetry: false),

            SafetyDecisionOutcome.ModalityDisabled => IsYoung(safetyBand)
                ? new ChildFacingMessage("We can't make that kind yet. Try another one!", CanRetry: false)
                : new ChildFacingMessage("That kind isn't available yet.", CanRetry: false),

            // Blocked, CheckUnavailable, and anything added later all land here. The default is the
            // safe wording rather than a throw: a new outcome must not be able to leak a technical
            // string to a child because someone forgot a switch arm.
            _ => IsYoung(safetyBand)
                ? new ChildFacingMessage("That one didn't work. Let's try again!", CanRetry: true)
                : new ChildFacingMessage("That one didn't work. Try again?", CanRetry: true),
        };

    private static bool IsYoung(string safetyBand)
        => safetyBand is SafetyBandNames.Under6 or SafetyBandNames.Age6To9
            // An unknown band gets the gentlest wording, matching every other default here.
            || !SafetyBandNames.All.Contains(safetyBand);
}

/// <param name="CanRetry">
/// Whether to offer "try again". True for a refusal, false for a hold — telling a child to retry
/// something an adult is already reviewing would produce a second copy and no explanation.
/// </param>
public sealed record ChildFacingMessage(string Text, bool CanRetry);
