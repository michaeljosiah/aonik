namespace Aonik.Cli;

public sealed class AonikCliException : Exception
{
    public AonikCliException(string message)
        : base(message)
    {
    }

    public AonikCliException(string message, int? statusCode, string? ruleId)
        : base(message)
    {
        StatusCode = statusCode;
        RuleId = ruleId;
    }

    /// <summary>HTTP status the API returned, when the failure came from a response rather than
    /// from the transport. Verification checks need this: "the call failed" is not the same claim
    /// as "the API rejected this input", and treating them alike lets a 500 pass for a 400.</summary>
    public int? StatusCode { get; }

    /// <summary>The Spec 066 rule id (V1, V2, …) when the body carried one.</summary>
    public string? RuleId { get; }
}
