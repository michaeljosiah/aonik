using System.Text.Json;

using Aonik.SharedKernel.Abstractions.Agents;

namespace Aonik.SharedKernel.Abstractions.Workflows;

public static class WorkflowFactsHash
{
    public static string Compute(WorkflowFactsSnapshot snapshot) => ToolArgumentsHash.Compute(new Dictionary<string, object?>
    {
        ["schema"] = snapshot.Schema,
        ["subjectVersion"] = snapshot.SubjectVersion,
        ["facts"] = new SortedDictionary<string, JsonElement>(snapshot.Facts.ToDictionary(pair => pair.Key, pair => pair.Value), StringComparer.Ordinal),
        ["blockReason"] = snapshot.BlockReason,
        ["subjectCreatedByUserId"] = snapshot.SubjectCreatedByUserId,
    });
}
