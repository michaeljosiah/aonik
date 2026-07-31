namespace Aonik.SharedKernel.Abstractions.Subscriptions;

/// <summary>
/// A subscriber has no remaining allowance for the requested work (Spec 087 §9, §10). Thrown by
/// <see cref="IUsageMeter"/> rather than returning a flag, because by the time the meter is called
/// the work is about to run — a caller that wants to ask first, without an exception, reads
/// <see cref="IEntitlementReader"/>.
///
/// Overage is always an explicit purchase, never an implicit debt: allowance is refused at zero
/// and never goes negative.
/// </summary>
public class EntitlementExceededException : Exception
{
    public EntitlementExceededException(string meterCode, decimal requested, decimal available)
        : base($"Entitlement '{meterCode}' has {available} remaining; {requested} was requested.")
    {
        MeterCode = meterCode;
        Requested = requested;
        Available = available;
    }

    /// <summary>The meter that refused the request.</summary>
    public string MeterCode { get; }

    /// <summary>How much was asked for.</summary>
    public decimal Requested { get; }

    /// <summary>How much was actually available across all open grants.</summary>
    public decimal Available { get; }
}
