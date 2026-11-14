using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions.Agents;

namespace Aonik.Agents.Services.Workflows;

internal sealed record PreparedWorkflowGovernance(
    WorkflowGovernanceCommand Command, WorkflowDefinition? Definition,
    Guid WorkflowId, Guid? TargetVersionId, ToolApprovalPreview Preview);

internal sealed class WorkflowGovernancePreparation(
    AgentsDbContext database, WorkflowAuthorization authorization, IWorkflowService workflows)
{
    public async Task<PreparedWorkflowGovernance> PrepareAsync(
        WorkflowGovernanceCommand command, bool retiring, CancellationToken cancellationToken = default)
    {
        await authorization.RequireAsync("Workflow.Publish", cancellationToken);
        await authorization.RequireAsync("Workflow.Read", cancellationToken);
        if (command is null || string.IsNullOrWhiteSpace(command.Slug) || command.Slug.Length > 100
            || command.ExpectedVersion is null || command.ExpectedVersion.Length > 128
            || command.DefinitionHash is not { Length: 64 } || command.DefinitionHash.Any(character => !char.IsAsciiHexDigitLower(character)))
            throw new ArgumentException("Provide the saved slug, expected version and exact SHA-256 definition hash.");
        Workflow workflow = await database.Workflows.AsNoTracking().SingleOrDefaultAsync(
            workflow => workflow.TenantId == authorization.TenantId && workflow.Slug == command.Slug, cancellationToken)
            ?? throw new KeyNotFoundException("Workflow not found.");
        WorkflowService.CheckVersion(workflow, command.ExpectedVersion);
        if (workflow.PublishedVersionId != command.ExpectedPublishedVersionId)
            throw new WorkflowConflictException("The published workflow version changed. Review it again.");
        WorkflowVersion? published = workflow.PublishedVersionId is { } id
            ? await database.WorkflowVersions.AsNoTracking().SingleOrDefaultAsync(
                version => version.TenantId == workflow.TenantId && version.WorkflowId == workflow.Id && version.Id == id, cancellationToken)
            : null;
        JsonElement? before = published?.DefinitionJson is { } publishedJson ? JsonSerializer.Deserialize<JsonElement>(publishedJson) : null;
        if (retiring)
        {
            if (published is null || workflow.RetiredAt.HasValue || command.TargetVersionId.HasValue)
                throw new WorkflowConflictException("Only a currently published workflow can be retired.");
            if (!string.Equals(command.DefinitionHash, published.DefinitionHash, StringComparison.Ordinal))
                throw new WorkflowConflictException("Retirement must reference the exact published definition hash.");
            return new(command, null, workflow.Id, published.Id,
                new($"Retire workflow {workflow.Slug} and prevent further effects", before,
                    JsonSerializer.SerializeToElement(new { state = "Retired", publishedVersionId = published.Id })));
        }

        WorkflowVersion? target;
        WorkflowDefinition definition;
        if (command.TargetVersionId is { } targetId)
        {
            target = await database.WorkflowVersions.AsNoTracking().SingleOrDefaultAsync(
                version => version.TenantId == workflow.TenantId && version.WorkflowId == workflow.Id && version.Id == targetId, cancellationToken)
                ?? throw new KeyNotFoundException("Workflow version not found.");
            if (target.PublishedAt is null || target.DefinitionJson is null || target.SchemaVersion != 1)
                throw new WorkflowConflictException("Rollback requires a previously published executable snapshot.");
            definition = JsonSerializer.Deserialize<WorkflowDefinition>(target.DefinitionJson, WorkflowDefinitionCodec.JsonOptions)
                ?? throw new WorkflowConflictException("Workflow snapshot is invalid.");
        }
        else
        {
            WorkflowGraphResponse graph = await workflows.GetBySlugAsync(workflow.Slug, cancellationToken)
                ?? throw new KeyNotFoundException("Workflow not found.");
            if (graph.ExpectedVersion != command.ExpectedVersion)
                throw new WorkflowConflictException("The draft changed while its publication was being prepared.");
            definition = WorkflowDefinitionCodec.FromGraph(graph);
            target = await database.WorkflowVersions.AsNoTracking().SingleOrDefaultAsync(
                version => version.TenantId == workflow.TenantId && version.WorkflowId == workflow.Id && version.Tag == workflow.Version, cancellationToken);
            // Historical metadata stays metadata. Approval will create a new snapshot if needed.
            if (target?.DefinitionJson is null) target = null;
        }

        string json = WorkflowDefinitionCodec.Serialize(definition);
        string hash = WorkflowDefinitionCodec.Hash(json);
        if (hash != command.DefinitionHash || (target is not null && target.DefinitionHash != hash))
            throw new WorkflowConflictException("The exact workflow definition hash no longer matches.");
        if (definition.Slug != workflow.Slug || definition.DefinitionKind != workflow.DefinitionKind)
            throw new WorkflowConflictException("The snapshot belongs to a different workflow definition.");
        IReadOnlyList<WorkflowDiagnostic> diagnostics = WorkflowDefinitionValidator.Validate(definition, forPublication: true);
        if (diagnostics.Any(diagnostic => diagnostic.Severity == "Error"))
            throw new ArgumentException("Publication validation failed: " + string.Join("; ", diagnostics.Where(diagnostic => diagnostic.Severity == "Error").Take(10).Select(diagnostic => diagnostic.Message)));
        return new(command, definition, workflow.Id, target?.Id,
            new($"Publish workflow {workflow.Slug}, definition {hash[..12]}", before, JsonSerializer.Deserialize<JsonElement>(json)));
    }
}
