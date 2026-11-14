using System.Text.Json;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Workflows.Graph;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Agents.Services.Workflows;

internal sealed class WorkflowPublicationValidator(
    WorkflowActionRegistry actions, ITenantRoleReader roles, ITenantProvider tenantProvider,
    WorkflowExecutionAuthorization authorization, IEnumerable<IConnectorDescriptor>? connectors = null,
    IConnectorService? connectorService = null, ICurrentUserProvider? currentUser = null, IUserDirectoryReader? users = null, IClock? clock = null,
    WorkflowDependencyValidator? dependencies = null)
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

    public async Task<IReadOnlyList<WorkflowDiagnostic>> ValidateAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default, Guid? workflowId = null)
    {
        var diagnostics = WorkflowDefinitionValidator.Validate(definition, forPublication: true).ToList();
        int maximumNodes = definition.SchemaVersion == 1 ? 30 : WorkflowControlLimits.MaximumGraphNodes;
        if (definition.Nodes.Count > maximumNodes) diagnostics.Add(new("ExecutableNodeLimit", "nodes", $"This workflow schema supports at most {maximumNodes} nodes."));
        if (diagnostics.Any(diagnostic => diagnostic.Severity == "Error")) return diagnostics;
        if (definition.Nodes.Any(WorkflowControlContracts.IsComposition))
        {
            try
            {
                if (dependencies is null || workflowId is null) throw new ArgumentException("Dependency validation requires a stored tenant workflow.");
                _ = await dependencies.ValidateAsync(definition, workflowId.Value, cancellationToken);
            }
            catch (Exception exception) when (exception is ArgumentException or JsonException or WorkflowConflictException)
            { diagnostics.Add(new("WorkflowDependency", "nodes", exception.Message)); }
        }
        Dictionary<string, HashSet<string>> dominators = Dominators(definition);
        foreach (WorkflowDefinitionNode node in definition.Nodes)
        {
            if (WorkflowDefinitionValidator.IsKind(node, WorkflowNodeKinds.Wait)
                && node.Parameters.Deserialize<WorkflowWaitParameters>(WorkflowDefinitionCodec.JsonOptions)?.UntilUtc is { } until
                && until > (clock?.UtcNow ?? DateTime.UtcNow).AddSeconds(WorkflowControlLimits.MaximumLifetimeSeconds))
                diagnostics.Add(new("WaitDeadline", node.Key, "A wait deadline must be within seven days at publication."));
            if (WorkflowDefinitionValidator.IsKind(node, WorkflowNodeKinds.Human))
            {
                WorkflowHumanReviewer reviewer = WorkflowHumanTaskContract.Parameters(node).Reviewer;
                Guid tenantId = tenantProvider.GetCurrentTenantId();
                if ((reviewer.RoleId is { } roleId && !await roles.ExistsAsync(tenantId, roleId, cancellationToken))
                    || (reviewer.UserId is { } userId && (users is null || !(await users.GetByIdsAsync(tenantId, [userId], cancellationToken))
                        .Any(user => user.UserId == userId && user.Status == "Active"))))
                    diagnostics.Add(new("HumanReviewer", node.Key, "Select a current tenant user or reviewer role."));
            }
            if (WorkflowDefinitionValidator.IsKind(node, WorkflowNodeKinds.Child))
            {
                WorkflowInputBinding binding = WorkflowControlContracts.Child(node).InvoiceId;
                ValidateChildBinding(binding, node.Key);
            }
            if (WorkflowDefinitionValidator.IsKind(node, WorkflowNodeKinds.Parallel))
                foreach (WorkflowParallelBranch branch in WorkflowControlContracts.Parallel(node).Branches) ValidateChildBinding(branch.InvoiceId, node.Key);
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
                ConnectorActionDescriptor? connector = connectors?.SelectMany(item => item.Descriptor.Actions)
                    .SingleOrDefault(action => action.Key == parameters.ActionKey && action.Version == parameters.ContractVersion);
                if (connector is not null)
                {
                    if (connectorService is null || workflowId is null || parameters.Connection is null || currentUser?.GetCurrentUserId() is not { } userId)
                        throw new ArgumentException("An exact authorized connection and workflow grant are required before publication.");
                    await connectorService.ValidateWorkflowBindingAsync(workflowId.Value, parameters.Connection, new(connector.Key, connector.Version), new("User", userId, userId), cancellationToken);
                }
                else if (parameters.Connection is not null) throw new ArgumentException("Native domain actions cannot carry connector bindings.");
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
                            || !(WorkflowDefinitionValidator.IsKind(prior, WorkflowNodeKinds.Tool) || WorkflowDefinitionValidator.IsKind(prior, WorkflowNodeKinds.Child)))
                            throw new ArgumentException("A referenced output must come from an action on every prior path.");
                        JsonElement schema;
                        if (WorkflowDefinitionValidator.IsKind(prior, WorkflowNodeKinds.Child)) schema = WorkflowControlContracts.ChildDescriptor.OutputSchema;
                        else
                        {
                            WorkflowToolParameters priorParameters = prior.Parameters.Deserialize<WorkflowToolParameters>(WorkflowDefinitionCodec.JsonOptions)!;
                            schema = actions.Resolve(priorParameters.ActionKey, priorParameters.ContractVersion).Descriptor.OutputSchema;
                        }
                        if (!schema.TryGetProperty("properties", out JsonElement properties) || !properties.TryGetProperty(binding.Output, out _))
                            throw new ArgumentException("The referenced output field is not declared by its action contract.");
                    }
                }
                if (parameters.Inputs.Values.All(binding => binding.Literal.HasValue))
                    handler.ValidateArguments(WorkflowInputBinder.Bind(parameters, new("fixture", new Dictionary<string, JsonElement>(), ""),
                        new Dictionary<string, SharedKernel.Abstractions.Workflows.WorkflowActionResult>()));
            }
            catch (Exception exception) when (exception is ArgumentException or JsonException or WorkflowConflictException or WorkflowExecutionException or ConnectorAccessException or PermissionDeniedException)
            {
                diagnostics.Add(new("ActionContract", node.Key, exception is WorkflowExecutionException ? "The action's module is unavailable." : exception.Message));
            }
        }
        return diagnostics;

        void ValidateChildBinding(WorkflowInputBinding binding, string nodeKey)
        {
            if (binding.Step is not { } source) return;
            WorkflowDefinitionNode? prior = definition.Nodes.SingleOrDefault(candidate => candidate.Key == source);
            JsonElement schema;
            if (prior is null || source == nodeKey || !dominators[nodeKey].Contains(source))
            { diagnostics.Add(new("ChildInput", nodeKey, "Child input must be a UUID output on every preceding path.")); return; }
            if (WorkflowDefinitionValidator.IsKind(prior, WorkflowNodeKinds.Tool))
            {
                WorkflowToolParameters parameters = prior.Parameters.Deserialize<WorkflowToolParameters>(WorkflowDefinitionCodec.JsonOptions)!;
                schema = actions.Resolve(parameters.ActionKey, parameters.ContractVersion).Descriptor.OutputSchema;
            }
            else if (WorkflowDefinitionValidator.IsKind(prior, WorkflowNodeKinds.Child)) schema = WorkflowControlContracts.ChildDescriptor.OutputSchema;
            else { diagnostics.Add(new("ChildInput", nodeKey, "Child input requires a declared UUID output.")); return; }
            if (!schema.TryGetProperty("properties", out JsonElement properties) || !properties.TryGetProperty(binding.Output!, out JsonElement field)
                || !field.TryGetProperty("type", out JsonElement type) || type.GetString() != "string"
                || !field.TryGetProperty("format", out JsonElement format) || format.GetString() != "uuid")
                diagnostics.Add(new("ChildInput", nodeKey, "The referenced child input is not a UUID field."));
        }
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
