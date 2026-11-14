using System.Text.Json;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Entities.Workflows;
using Aonik.SharedKernel.Abstractions.Workflows;

namespace Aonik.Agents.Services.Workflows;

internal static class WorkflowSubjectContract
{
    public static string Type(WorkflowDefinition definition)
    {
        WorkflowDefinitionNode trigger = definition.Nodes.Single(node => WorkflowDefinitionValidator.IsKind(node, WorkflowNodeKinds.Trigger));
        if (trigger.Parameters.ValueKind != JsonValueKind.Object || !trigger.Parameters.TryGetProperty("subjectType", out JsonElement type))
            return WorkflowSubjects.Invoice;
        if (type.ValueKind != JsonValueKind.String || !WorkflowSubjects.IsSupported(type.GetString())
            || (type.GetString() == WorkflowSubjects.Document && definition.SchemaVersion != 2))
            throw new ArgumentException("Document subjects require an explicit schema 2 trigger subjectType; existing definitions retain Invoice semantics.");
        return type.GetString()!;
    }

    public static IReadOnlyList<DecisionFactField> Facts(WorkflowDefinition definition) => Facts(Type(definition));
    public static IReadOnlyList<DecisionFactField> Facts(string subjectType) => subjectType == WorkflowSubjects.Invoice
        ? DecisionTableEvaluator.FactFields() : [new("document.id", "Id", true), new("document.fileId", "Id", true), new("document.ownerPartyId", "Id", true)];
}
