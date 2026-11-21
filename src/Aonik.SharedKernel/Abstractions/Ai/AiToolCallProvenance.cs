using System.Runtime.CompilerServices;

using Microsoft.Extensions.AI;

namespace Aonik.SharedKernel.Abstractions.Ai;

/// <summary>Associates an audited model response with the exact tool call consumed by the function invoker.</summary>
public static class AiToolCallProvenance
{
    private static readonly ConditionalWeakTable<FunctionCallContent, Origin> Origins = new();

    public static Guid? CurrentRunId => FunctionInvokingChatClient.CurrentContext?.CallContent is { } call
        && Origins.TryGetValue(call, out Origin? origin) ? origin.RunId : null;

    internal static void Record(FunctionCallContent call, Guid runId)
    {
        // The reference stays on the live response. Model arguments and serialized history cannot supply it.
        if (runId == Guid.Empty) Origins.Remove(call);
        else Origins.AddOrUpdate(call, new Origin(runId));
    }

    private sealed record Origin(Guid RunId);
}
