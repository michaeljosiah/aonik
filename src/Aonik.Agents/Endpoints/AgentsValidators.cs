using Aonik.Agents.Contracts.Agui;
using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.SharedKernel.Validation;
using FastEndpoints;
using FluentValidation;

namespace Aonik.Agents.Endpoints;

// ────────────────────────────────────────────────────────────────────
// Validators for the Agents module's request DTOs. FastEndpoints
// auto-discovers these and runs them before HandleAsync, returning a
// 400 with FluentValidation errors when input is invalid.
// ────────────────────────────────────────────────────────────────────

// ── AGUI streaming ──────────────────────────────────────────────────

public sealed class AguiRunInputValidator : Validator<AguiRunInput>
{
    public AguiRunInputValidator()
    {
        RuleFor(x => x.ThreadId).MaximumLength(256);
        RuleFor(x => x.RunId).MaximumLength(256);
        RuleFor(x => x.AgentId).MaximumLength(128);
        RuleFor(x => x.Messages)
            .Must(m => m == null || m.Count <= 200)
            .WithMessage("AGUI run may include at most 200 messages.");
        RuleFor(x => x.AudioFormat)
            .Must(fmt => fmt is null or "mp3" or "opus" or "wav")
            .WithMessage("AudioFormat must be one of: mp3, opus, wav.")
            .When(x => x.VoiceMode);
    }
}

// ── Chat ────────────────────────────────────────────────────────────

public sealed class ChatRequestValidator : Validator<ChatRequest>
{
    public ChatRequestValidator()
    {
        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("Message is required.")
            .MaximumLength(64_000).WithMessage("Message may not exceed 64,000 characters.");
        RuleFor(x => x.SessionId).MaximumLength(256);
        RuleFor(x => x.ThreadId).MaximumLength(256);
    }
}

// ── Playground ──────────────────────────────────────────────────────

public sealed class PlaygroundRunRequestValidator : Validator<PlaygroundRunRequest>
{
    public PlaygroundRunRequestValidator()
    {
        RuleFor(x => x.AgentName).MaximumLength(128);
        RuleFor(x => x.SystemPrompt).MaximumLength(64_000);
        RuleFor(x => x.UserBriefJson).MaximumLength(128_000);
        RuleFor(x => x.ModelId).ValidIdWhenSupplied();
        RuleFor(x => x.AiTaskId).ValidIdWhenSupplied();
        RuleFor(x => x.Temperature)
            .InclusiveBetween(0f, 2f).WithMessage("Temperature must be between 0 and 2.")
            .When(x => x.Temperature.HasValue);
        RuleFor(x => x.MaxTokens)
            .InclusiveBetween(1, 200_000).WithMessage("MaxTokens must be between 1 and 200000.")
            .When(x => x.MaxTokens.HasValue);
        RuleFor(x => x.EnabledToolNames)
            .Must(t => t == null || t.Count <= 200)
            .WithMessage("EnabledToolNames may include at most 200 entries.");
        RuleFor(x => x.Messages)
            .Must(m => m == null || m.Count <= 200)
            .WithMessage("Messages may include at most 200 entries.");
        RuleFor(x => x.PromptVariables)
            .Must(v => v == null || v.Count <= 256)
            .WithMessage("PromptVariables may include at most 256 entries.");
    }
}

// ── Workflows ───────────────────────────────────────────────────────

public sealed class WorkflowRequestValidator : Validator<WorkflowRequest>
{
    public WorkflowRequestValidator()
    {
        RuleFor(x => x.WorkflowName).RequiredText(128);
        RuleFor(x => x.Input)
            .NotEmpty().WithMessage("Workflow input is required.")
            .MaximumLength(128_000).WithMessage("Workflow input may not exceed 128,000 characters.");
    }
}

public sealed class WorkflowSaveRequestValidator : Validator<WorkflowSaveRequest>
{
    public WorkflowSaveRequestValidator()
    {
        RuleFor(x => x.Slug)
            .RequiredText(128)
            .Matches("^[a-z0-9][a-z0-9-]*$")
            .WithMessage("Slug must be lowercase alphanumerics and hyphens, starting with a letter or digit.");
        RuleFor(x => x.Name).RequiredText(256);
        RuleFor(x => x.Description).MaximumLength(2048);
        RuleFor(x => x.State).RequiredText(32);
        RuleFor(x => x.Version).RequiredText(64);
        RuleFor(x => x.OwnerColor).MaximumLength(32);
        RuleFor(x => x.OwnerAgentId).ValidIdWhenSupplied();
        RuleFor(x => x.Contributors)
            .NotNull().WithMessage("Contributors collection is required (may be empty).")
            .Must(c => c.Count <= 64).WithMessage("Workflow may have at most 64 contributors.");
        RuleForEach(x => x.Contributors).RequiredId();
        RuleFor(x => x.Nodes)
            .NotNull().WithMessage("Nodes collection is required (may be empty).")
            .Must(n => n.Count <= 500).WithMessage("Workflow may have at most 500 nodes.");
        RuleForEach(x => x.Nodes).SetValidator(new WorkflowSaveNodeValidator());
        RuleFor(x => x.Edges)
            .NotNull().WithMessage("Edges collection is required (may be empty).")
            .Must(e => e.Count <= 2000).WithMessage("Workflow may have at most 2000 edges.");
        RuleForEach(x => x.Edges).SetValidator(new WorkflowSaveEdgeValidator());
        RuleFor(x => x.VersionMessage).MaximumLength(1024);
    }
}

public sealed class WorkflowSaveNodeValidator : Validator<WorkflowSaveNode>
{
    public WorkflowSaveNodeValidator()
    {
        RuleFor(x => x.ClientId).RequiredText(128);
        RuleFor(x => x.Kind).RequiredText(64);
        RuleFor(x => x.Label).RequiredText(256);
        RuleFor(x => x.Summary).MaximumLength(2048);
        RuleFor(x => x.Notes).MaximumLength(8192);
        RuleFor(x => x.X).InclusiveBetween(-100_000, 100_000);
        RuleFor(x => x.Y).InclusiveBetween(-100_000, 100_000);
        RuleFor(x => x.ParamsJson).MaximumLength(64_000);
    }
}

public sealed class WorkflowSaveEdgeValidator : Validator<WorkflowSaveEdge>
{
    public WorkflowSaveEdgeValidator()
    {
        RuleFor(x => x.FromClientId).RequiredText(128);
        RuleFor(x => x.ToClientId).RequiredText(128);
        RuleFor(x => x.FromIndex).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Label).MaximumLength(256);
    }
}

// ── Agent runs / configurations ─────────────────────────────────────

public sealed class ApproveProposalRequestValidator : Validator<ApproveProposalRequest>
{
    public ApproveProposalRequestValidator() => RuleFor(x => x.Id).RequiredId();
}

public sealed class DismissProposalRequestValidator : Validator<DismissProposalRequest>
{
    public DismissProposalRequestValidator() => RuleFor(x => x.Id).RequiredId();
}

public sealed class GetProposalRequestValidator : Validator<GetProposalRequest>
{
    public GetProposalRequestValidator() => RuleFor(x => x.Id).RequiredId();
}

public sealed class ArchiveChatThreadRequestValidator : Validator<ArchiveChatThreadRequest>
{
    public ArchiveChatThreadRequestValidator() => RuleFor(x => x.ThreadId).RequiredId();
}

public sealed class GetChatThreadRequestValidator : Validator<GetChatThreadRequest>
{
    public GetChatThreadRequestValidator() => RuleFor(x => x.ThreadId).RequiredId();
}

public sealed class ListChatThreadsRequestValidator : Validator<ListChatThreadsRequest>
{
    public ListChatThreadsRequestValidator()
    {
        RuleFor(x => x.Page).PageNumber();
        RuleFor(x => x.PageSize).PageSize();
    }
}

public sealed class ListAgentRunsRequestValidator : Validator<ListAgentRunsRequest>
{
    public ListAgentRunsRequestValidator()
    {
        RuleFor(x => x.AgentId).RequiredId();
        RuleFor(x => x.Page).PageNumber();
        RuleFor(x => x.PageSize).PageSize();
    }
}

public sealed class GetAgentConfigurationRequestValidator : Validator<GetAgentConfigurationRequest>
{
    public GetAgentConfigurationRequestValidator() => RuleFor(x => x.Name).RequiredText(128);
}

public sealed class DeleteAgentConfigurationRequestValidator : Validator<DeleteAgentConfigurationRequest>
{
    public DeleteAgentConfigurationRequestValidator() => RuleFor(x => x.Name).RequiredText(128);
}

public sealed class ResetAgentPromptRequestValidator : Validator<ResetAgentPromptRequest>
{
    public ResetAgentPromptRequestValidator() => RuleFor(x => x.Name).RequiredText(128);
}

public sealed class UpsertAgentConfigurationEndpointRequestValidator : Validator<UpsertAgentConfigurationEndpointRequest>
{
    public UpsertAgentConfigurationEndpointRequestValidator()
    {
        RuleFor(x => x.Name).RequiredText(128);
        RuleFor(x => x.Description).MaximumLength(2048);
        RuleFor(x => x.InstructionsText).MaximumLength(64_000);
        RuleFor(x => x.ToolsetIdsJson).MaximumLength(64_000);
        RuleFor(x => x.PermissionsProfileJson).MaximumLength(64_000);
        RuleFor(x => x.RiskTier)
            .Must(t => t is null or "Low" or "Medium" or "High" or "Critical")
            .WithMessage("RiskTier must be one of: Low, Medium, High, Critical.");
        RuleFor(x => x.ModelId).ValidIdWhenSupplied();
        RuleFor(x => x.IconUrl).MaximumLength(2048);
    }
}

public sealed class ImprovePromptRequestValidator : Validator<ImprovePromptRequest>
{
    public ImprovePromptRequestValidator()
    {
        RuleFor(x => x.CurrentPrompt).MaximumLength(64_000);
        RuleFor(x => x.UserIntent)
            .NotEmpty().WithMessage("User intent is required.")
            .MaximumLength(8_000).WithMessage("User intent may not exceed 8,000 characters.");
    }
}

public sealed class ProjectUserBriefRequestValidator : Validator<ProjectUserBriefRequest>
{
    public ProjectUserBriefRequestValidator()
    {
        RuleFor(x => x.UserId).RequiredId();
        RuleFor(x => x.PartyId).RequiredId();
    }
}

// ── Chat metrics / voice events ─────────────────────────────────────

public sealed class ChatClientMetricsRequestValidator : Validator<ChatClientMetricsRequest>
{
    public ChatClientMetricsRequestValidator()
    {
        RuleFor(x => x.ClientRoundTripMs).InclusiveBetween(0, 10 * 60 * 1000)
            .WithMessage("ClientRoundTripMs must be between 0 and 600000.");
        RuleFor(x => x.ClientTtftMs).InclusiveBetween(0, 10 * 60 * 1000)
            .WithMessage("ClientTtftMs must be between 0 and 600000.");
        RuleFor(x => x.ServerLatencyMs).InclusiveBetween(0, 10 * 60 * 1000);
        RuleFor(x => x.ServerTtftMs).InclusiveBetween(0, 10 * 60 * 1000);
        RuleFor(x => x.InputTokens).InclusiveBetween(0, 10_000_000);
        RuleFor(x => x.OutputTokens).InclusiveBetween(0, 10_000_000);
        RuleFor(x => x.ThreadId).MaximumLength(256);
        RuleFor(x => x.RunId).MaximumLength(256);
    }
}

public sealed class ChatVoiceClientEventRequestValidator : Validator<ChatVoiceClientEventRequest>
{
    public ChatVoiceClientEventRequestValidator()
    {
        RuleFor(x => x.EventName).RequiredText(128);
        RuleFor(x => x.ClientElapsedMs)
            .InclusiveBetween(0, 60 * 60 * 1000)
            .When(x => x.ClientElapsedMs.HasValue);
        RuleFor(x => x.ThreadId).MaximumLength(256);
        RuleFor(x => x.RunId).MaximumLength(256);
        RuleFor(x => x.AgentName).MaximumLength(128);
        RuleFor(x => x.VoiceTurnId).GreaterThanOrEqualTo(0).When(x => x.VoiceTurnId.HasValue);
        RuleFor(x => x.Stage).MaximumLength(64);
        RuleFor(x => x.Reason).MaximumLength(1024);
        RuleFor(x => x.Details)
            .Must(d => d == null || d.Count <= 64)
            .WithMessage("Details may include at most 64 entries.");
    }
}
