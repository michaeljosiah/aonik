using System.Text.Json;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Workflows.Graph;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;

namespace Aonik.Agents.Services.Workflows;

internal sealed class WorkflowPublicationValidator(
    WorkflowActionRegistry actions, ITenantRoleReader roles, ITenantProvider tenantProvider,
    WorkflowExecutionAuthorization authorization)
{
    public async Task<IReadOnlyList<WorkflowActionCapability>> CapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        var available = new List<WorkflowActionCapability>();
        foreach (var descriptor in actions.Descriptors)
        {
            try { await authorization.RequireModuleAsync(actions.Resolve(descriptor.Key, descriptor.Version).GetType(), cancellationToken); }
            catch (WorkflowExecutionException) { continue; }
            available.Add(new(descriptor.Key, descriptor.Version, descriptor.RequiredPermission, descriptor.Approval?.Tier.ToString() ?? "Read",
                descriptor.InputSchema, descriptor.OutputSchema));
        }
        return available;
    }

    public async Task<IReadOnlyList<WorkflowDiagnostic>> ValidateAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default)
    {
        var diagnostics = WorkflowDefinitionValidator.Validate(definition, forPublication: true).ToList();
        if (definition.Nodes.Count > 30) diagnostics.Add(new("ExecutableNodeLimit", "nodes", "Executable workflows support at most 30 nodes."));
        if (diagnostics.Any(diagnostic => diagnostic.Severity == "Error")) return diagnostics;
        Dictionary<string, HashSet<string>> dominators = Dominators(definition);
        foreach (WorkflowDefinitionNode node in definition.Nodes)
        {
            if (WorkflowDefinitionValidator.IsKind(node, WorkflowNodeKinds.Decision))
            {
                DecisionTable table = node.Parameters.Deserialize<DecisionTable>(WorkflowDefinitionCodec.JsonOptions)!;
                if (table.Rows.Count > 50 || table.Rows.Any(row => row.All.Count > 8))
                    diagnostics.Add(new("DecisionLimit", node.Key, "Executable decisions support at most 50 rows and eight comparisons per row."));
                foreach (DecisionRow row in table.Rows)
                    if (row.All.Any(comparison => comparison.Field == "invoice.total" && comparison.Operator is "gt" or "gte" or "lt" or "lte")
                        && !row.All.Any(comparison => comparison.Field == "invoice.currency" && comparison.Operator is "eq" or "in"))
                        diagnostics.Add(new("CurrencyRequired", $"{node.Key}.{row.Id}", "An amount threshold must explicitly select its invoice currency."));
            }
            if (!WorkflowDefinitionValidator.IsKind(node, WorkflowNodeKinds.Tool)) continue;
            try
            {
                WorkflowToolParameters parameters = node.Parameters.Deserialize<WorkflowToolParameters>(WorkflowDefinitionCodec.JsonOptions)
                    ?? throw new ArgumentException("Action parameters are required.");
                var handler = actions.Resolve(parameters.ActionKey, parameters.ContractVersion);
                await authorization.RequireModuleAsync(handler.GetType(), cancellationToken);
                WorkflowInputBinder.ValidateNames(parameters, handler.Descriptor);
                if (handler.Descriptor.Approval?.Tier is ToolApprovalTier.Medium or ToolApprovalTier.High)
                {
                    if (parameters.Reviewer is not { RoleId: var roleId } || roleId == Guid.Empty
                        || !await roles.ExistsAsync(tenantProvider.GetCurrentTenantId(), roleId, cancellationToken))
                        throw new ArgumentException("A current tenant reviewer role is required for this action.");
                }
                foreach ((string input, WorkflowInputBinding binding) in parameters.Inputs)
                {
                    if (binding is null) throw new ArgumentException("Action bindings cannot be null.");
                    int count = (binding.Literal.HasValue ? 1 : 0) + (binding.Fact is not null ? 1 : 0)
                        + (binding.Step is not null || binding.Output is not null ? 1 : 0);
                    if (count != 1) throw new ArgumentException("Each input selects one literal, fact or prior output.");
                    if ((binding.Step is null) != (binding.Output is null))
                        throw new ArgumentException("A prior output binding requires both its step and output field.");
                    if (binding.Fact is { } fact)
                    {
                        DecisionFactField field = DecisionTableEvaluator.FactFields().SingleOrDefault(field => field.Name == fact)
                            ?? throw new ArgumentException("The action references an unknown invoice fact.");
                        JsonElement inputSchema = handler.Descriptor.InputSchema.GetProperty("properties").GetProperty(input);
                        string type = field.Type == "Boolean" ? "boolean" : "string";
                        if (inputSchema.TryGetProperty("type", out JsonElement declared)
                            && !(declared.ValueKind == JsonValueKind.String ? declared.GetString() == type : declared.EnumerateArray().Any(item => item.GetString() == type)))
                            throw new ArgumentException("The invoice fact type does not match its action input.");
                        if (inputSchema.TryGetProperty("format", out JsonElement format) && format.GetString() == "uuid" && field.Type != "Id")
                            throw new ArgumentException("An identifier action input requires an identifier fact.");
                    }
                    if (binding.Step is { } source)
                    {
                        WorkflowDefinitionNode? prior = definition.Nodes.SingleOrDefault(candidate => candidate.Key == source);
                        if (source == node.Key || prior is null || binding.Output is null || !dominators[node.Key].Contains(source)
                            || !WorkflowDefinitionValidator.IsKind(prior, WorkflowNodeKinds.Tool))
                            throw new ArgumentException("A referenced output must come from an action on every prior path.");
                        WorkflowToolParameters priorParameters = prior.Parameters.Deserialize<WorkflowToolParameters>(WorkflowDefinitionCodec.JsonOptions)!;
                        JsonElement schema = actions.Resolve(priorParameters.ActionKey, priorParameters.ContractVersion).Descriptor.OutputSchema;
                        if (!schema.TryGetProperty("properties", out JsonElement properties) || !properties.TryGetProperty(binding.Output, out _))
                            throw new ArgumentException("The referenced output field is not declared by its action contract.");
                    }
                }
                if (parameters.Inputs.Values.All(binding => binding.Literal.HasValue))
                    handler.ValidateArguments(WorkflowInputBinder.Bind(parameters, new("fixture", new Dictionary<string, JsonElement>(), ""),
                        new Dictionary<string, SharedKernel.Abstractions.Workflows.WorkflowActionResult>()));
            }
            catch (Exception exception) when (exception is ArgumentException or JsonException or WorkflowConflictException or WorkflowExecutionException)
            {
                diagnostics.Add(new("ActionContract", node.Key, exception is WorkflowExecutionException ? "The action's module is unavailable." : exception.Message));
            }
        }
        return diagnostics;
    }

    private static Dictionary<string, HashSet<string>> Dominators(WorkflowDefinition definition)
    {
        string start = definition.Nodes.Single(node => WorkflowDefinitionValidator.IsKind(node, WorkflowNodeKinds.Trigger)).Key;
        HashSet<string> all = definition.Nodes.Select(node => node.Key).ToHashSet(StringComparer.Ordinal);
        Dictionary<string, HashSet<string>> result = definition.Nodes.ToDictionary(node => node.Key,
            node => node.Key == start ? new HashSet<string>([start], StringComparer.Ordinal) : new HashSet<string>(all, StringComparer.Ordinal), StringComparer.Ordinal);
        for (int pass = 0; pass < definition.Nodes.Count; pass++)
            foreach (string key in all.Where(key => key != start))
            {
                var intersection = new HashSet<string>(all, StringComparer.Ordinal);
                foreach (string parent in definition.Edges.Where(edge => edge.To == key).Select(edge => edge.From)) intersection.IntersectWith(result[parent]);
                intersection.Add(key);
                result[key] = intersection;
            }
        return result;
    }
}
