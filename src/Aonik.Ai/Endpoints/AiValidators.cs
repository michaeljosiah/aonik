using Aonik.Ai.Contracts.Models;
using Aonik.SharedKernel.Validation;
using FastEndpoints;
using FluentValidation;

namespace Aonik.Ai.Endpoints;

// ────────────────────────────────────────────────────────────────────
// Validators for the Ai module's request DTOs.
// ────────────────────────────────────────────────────────────────────

// ── Models ──────────────────────────────────────────────────────────

internal sealed class CreateAiModelRequestValidator : Validator<CreateAiModelRequest>
{
    public CreateAiModelRequestValidator()
    {
        RuleFor(x => x.AiProviderId).RequiredId();
        RuleFor(x => x.ModelName).RequiredText(256);
        RuleFor(x => x.ContextWindow).InclusiveBetween(0, 10_000_000);
        RuleFor(x => x.CostProfileJson).MaximumLength(64_000);
        RuleFor(x => x.LatencyProfileJson).MaximumLength(64_000);
        RuleFor(x => x.PolicyTagsJson).MaximumLength(16_000);
    }
}

internal sealed class UpdateAiModelEndpointRequestValidator : Validator<UpdateAiModelEndpointRequest>
{
    public UpdateAiModelEndpointRequestValidator()
    {
        RuleFor(x => x.ModelId).RequiredId();
        RuleFor(x => x.ModelName).MaximumLength(256);
        RuleFor(x => x.ContextWindow)
            .InclusiveBetween(0, 10_000_000)
            .When(x => x.ContextWindow.HasValue);
        RuleFor(x => x.CostProfileJson).MaximumLength(64_000);
        RuleFor(x => x.LatencyProfileJson).MaximumLength(64_000);
        RuleFor(x => x.PolicyTagsJson).MaximumLength(16_000);
    }
}

internal sealed class GetAiModelRequestValidator : Validator<GetAiModelRequest>
{
    public GetAiModelRequestValidator() => RuleFor(x => x.ModelId).RequiredId();
}

internal sealed class DeleteAiModelRequestValidator : Validator<DeleteAiModelRequest>
{
    public DeleteAiModelRequestValidator() => RuleFor(x => x.ModelId).RequiredId();
}

internal sealed class ListAiModelsRequestValidator : Validator<ListAiModelsRequest>
{
    public ListAiModelsRequestValidator() => RuleFor(x => x.ProviderId).ValidIdWhenSupplied();
}

// ── Providers ───────────────────────────────────────────────────────

internal sealed class CreateAiProviderRequestValidator : Validator<CreateAiProviderRequest>
{
    public CreateAiProviderRequestValidator()
    {
        RuleFor(x => x.Name).RequiredText(128);
        RuleFor(x => x.AuthConfigRef).MaximumLength(256);
        RuleFor(x => x.CapabilitiesJson).RequiredText(16_000);
    }
}

internal sealed class UpdateAiProviderEndpointRequestValidator : Validator<UpdateAiProviderEndpointRequest>
{
    public UpdateAiProviderEndpointRequestValidator()
    {
        RuleFor(x => x.ProviderId).RequiredId();
        RuleFor(x => x.Name).MaximumLength(128);
        RuleFor(x => x.AuthConfigRef).MaximumLength(256);
        RuleFor(x => x.CapabilitiesJson).MaximumLength(16_000);
    }
}

internal sealed class GetAiProviderRequestValidator : Validator<GetAiProviderRequest>
{
    public GetAiProviderRequestValidator() => RuleFor(x => x.ProviderId).RequiredId();
}

internal sealed class DeleteAiProviderRequestValidator : Validator<DeleteAiProviderRequest>
{
    public DeleteAiProviderRequestValidator() => RuleFor(x => x.ProviderId).RequiredId();
}

// ── AI Tasks ────────────────────────────────────────────────────────

internal sealed class CreateAiTaskRequestValidator : Validator<CreateAiTaskRequest>
{
    public CreateAiTaskRequestValidator()
    {
        RuleFor(x => x.UseCase).RequiredText(128);
        RuleFor(x => x.DisplayName).RequiredText(256);
        RuleFor(x => x.Description).MaximumLength(2048);
        RuleFor(x => x.Category).RequiredText(64);
        RuleFor(x => x.ExecutionMode).RequiredText(64);
        RuleFor(x => x.PromptName).RequiredText(128);
        RuleFor(x => x.PromptVersion).RequiredText(64);
        RuleFor(x => x.SystemTemplate)
            .NotEmpty().WithMessage("System template is required.")
            .MaximumLength(64_000);
        RuleFor(x => x.UserTemplate).MaximumLength(64_000);
        RuleFor(x => x.DeveloperTemplate).MaximumLength(64_000);
        RuleFor(x => x.VariablesSchemaJson).MaximumLength(64_000);
        RuleFor(x => x.OutputSchemaJson).MaximumLength(64_000);
        RuleFor(x => x.PrimaryModelId).ValidIdWhenSupplied();
    }
}

internal sealed class UpdateAiTaskEndpointRequestValidator : Validator<UpdateAiTaskEndpointRequest>
{
    public UpdateAiTaskEndpointRequestValidator()
    {
        RuleFor(x => x.TaskId).RequiredId();
        RuleFor(x => x.DisplayName).MaximumLength(256);
        RuleFor(x => x.Description).MaximumLength(2048);
        RuleFor(x => x.Category).MaximumLength(64);
        RuleFor(x => x.ExecutionMode).MaximumLength(64);
        RuleFor(x => x.PromptName).MaximumLength(128);
        RuleFor(x => x.PromptVersion).MaximumLength(64);
        RuleFor(x => x.SystemTemplate).MaximumLength(64_000);
        RuleFor(x => x.UserTemplate).MaximumLength(64_000);
        RuleFor(x => x.DeveloperTemplate).MaximumLength(64_000);
        RuleFor(x => x.VariablesSchemaJson).MaximumLength(64_000);
        RuleFor(x => x.OutputSchemaJson).MaximumLength(64_000);
        RuleFor(x => x.PrimaryModelId).ValidIdWhenSupplied();
    }
}

internal sealed class GetAiTaskRequestValidator : Validator<GetAiTaskRequest>
{
    public GetAiTaskRequestValidator() => RuleFor(x => x.TaskId).RequiredId();
}

internal sealed class DeleteAiTaskRequestValidator : Validator<DeleteAiTaskRequest>
{
    public DeleteAiTaskRequestValidator() => RuleFor(x => x.TaskId).RequiredId();
}

internal sealed class ResetAiTaskPromptRequestValidator : Validator<ResetAiTaskPromptRequest>
{
    public ResetAiTaskPromptRequestValidator() => RuleFor(x => x.TaskId).RequiredId();
}

// ── Prompt Specs ────────────────────────────────────────────────────

internal sealed class CreatePromptSpecRequestValidator : Validator<CreatePromptSpecRequest>
{
    public CreatePromptSpecRequestValidator()
    {
        RuleFor(x => x.Name).RequiredText(128);
        RuleFor(x => x.Version).RequiredText(64);
        RuleFor(x => x.SystemTemplate)
            .NotEmpty().WithMessage("System template is required.")
            .MaximumLength(64_000);
        RuleFor(x => x.UserTemplate).MaximumLength(64_000);
        RuleFor(x => x.DeveloperTemplate).MaximumLength(64_000);
        RuleFor(x => x.VariablesSchemaJson).MaximumLength(64_000);
        RuleFor(x => x.OutputSchemaJson).MaximumLength(64_000);
        RuleFor(x => x.SafetyPolicyRef).MaximumLength(256);
    }
}

internal sealed class UpdatePromptSpecEndpointRequestValidator : Validator<UpdatePromptSpecEndpointRequest>
{
    public UpdatePromptSpecEndpointRequestValidator()
    {
        RuleFor(x => x.PromptId).RequiredId();
        RuleFor(x => x.SystemTemplate).MaximumLength(64_000);
        RuleFor(x => x.UserTemplate).MaximumLength(64_000);
        RuleFor(x => x.DeveloperTemplate).MaximumLength(64_000);
        RuleFor(x => x.VariablesSchemaJson).MaximumLength(64_000);
        RuleFor(x => x.OutputSchemaJson).MaximumLength(64_000);
        RuleFor(x => x.SafetyPolicyRef).MaximumLength(256);
    }
}

internal sealed class GetPromptSpecRequestValidator : Validator<GetPromptSpecRequest>
{
    public GetPromptSpecRequestValidator() => RuleFor(x => x.PromptId).RequiredId();
}

internal sealed class DeletePromptSpecRequestValidator : Validator<DeletePromptSpecRequest>
{
    public DeletePromptSpecRequestValidator() => RuleFor(x => x.PromptId).RequiredId();
}

// ── Route Policies ──────────────────────────────────────────────────

internal sealed class CreateRoutePolicyRequestValidator : Validator<CreateRoutePolicyRequest>
{
    public CreateRoutePolicyRequestValidator()
    {
        RuleFor(x => x.UseCase).RequiredText(128);
        RuleFor(x => x.RiskTier)
            .NotEmpty().WithMessage("RiskTier is required.")
            .Must(t => AiValidatorConstants.AllowedRiskTiers.Contains(t))
            .WithMessage($"RiskTier must be one of: {string.Join(", ", AiValidatorConstants.AllowedRiskTiers)}.");
        RuleFor(x => x.DataSensitivity)
            .NotEmpty().WithMessage("DataSensitivity is required.")
            .Must(s => AiValidatorConstants.AllowedDataSensitivities.Contains(s))
            .WithMessage($"DataSensitivity must be one of: {string.Join(", ", AiValidatorConstants.AllowedDataSensitivities)}.");
        RuleFor(x => x.CostCeiling).NonNegativeMoney();
        RuleFor(x => x.PrimaryModelId).RequiredId();
        RuleFor(x => x.FallbackModelIdsJson).MaximumLength(16_000);
    }
}

internal sealed class UpdateRoutePolicyEndpointRequestValidator : Validator<UpdateRoutePolicyEndpointRequest>
{
    public UpdateRoutePolicyEndpointRequestValidator()
    {
        RuleFor(x => x.PolicyId).RequiredId();
        RuleFor(x => x.RiskTier)
            .Must(t => t == null || AiValidatorConstants.AllowedRiskTiers.Contains(t))
            .WithMessage($"RiskTier must be one of: {string.Join(", ", AiValidatorConstants.AllowedRiskTiers)}.");
        RuleFor(x => x.DataSensitivity)
            .Must(s => s == null || AiValidatorConstants.AllowedDataSensitivities.Contains(s))
            .WithMessage($"DataSensitivity must be one of: {string.Join(", ", AiValidatorConstants.AllowedDataSensitivities)}.");
        RuleFor(x => x.CostCeiling)
            .NonNegativeMoney()
            .When(x => x.CostCeiling.HasValue);
        RuleFor(x => x.PrimaryModelId).ValidIdWhenSupplied();
        RuleFor(x => x.FallbackModelIdsJson).MaximumLength(16_000);
    }
}

internal sealed class GetRoutePolicyRequestValidator : Validator<GetRoutePolicyRequest>
{
    public GetRoutePolicyRequestValidator() => RuleFor(x => x.PolicyId).RequiredId();
}

internal sealed class DeleteRoutePolicyRequestValidator : Validator<DeleteRoutePolicyRequest>
{
    public DeleteRoutePolicyRequestValidator() => RuleFor(x => x.PolicyId).RequiredId();
}

internal sealed class UpdateAiPolicyEndpointRequestValidator : Validator<UpdateAiPolicyEndpointRequest>
{
    public UpdateAiPolicyEndpointRequestValidator() => RuleFor(x => x.Id).RequiredId();
}

// ── Catalog ─────────────────────────────────────────────────────────

internal sealed class ListAiCatalogModelsRequestValidator : Validator<ListAiCatalogModelsRequest>
{
    public ListAiCatalogModelsRequestValidator()
    {
        RuleFor(x => x.ModelProviderKey).RequiredText(128);
    }
}

internal sealed class ImportAiCatalogModelProviderEndpointRequestValidator : Validator<ImportAiCatalogModelProviderEndpointRequest>
{
    public ImportAiCatalogModelProviderEndpointRequestValidator()
    {
        RuleFor(x => x.ModelProviderKey).RequiredText(128);
    }
}

// ── Traces ──────────────────────────────────────────────────────────

internal sealed class GetAiTraceRequestValidator : Validator<GetAiTraceRequest>
{
    public GetAiTraceRequestValidator() => RuleFor(x => x.RunId).RequiredId();
}

internal sealed class ListAiTracesRequestValidator : Validator<ListAiTracesRequest>
{
    public ListAiTracesRequestValidator()
    {
        RuleFor(x => x.Page).PageNumber().When(x => x.Page.HasValue);
        RuleFor(x => x.PageSize).PageSize(1, 500).When(x => x.PageSize.HasValue);
        RuleFor(x => x.UseCase).MaximumLength(128);
        RuleFor(x => x.Outcome).MaximumLength(64);
        RuleFor(x => x.TimeRange).MaximumLength(32);
        RuleFor(x => x.RunId).ValidIdWhenSupplied();
    }
}

internal sealed class ListAiTraceObservationsRequestValidator : Validator<ListAiTraceObservationsRequest>
{
    public ListAiTraceObservationsRequestValidator()
    {
        RuleFor(x => x.Page).PageNumber().When(x => x.Page.HasValue);
        RuleFor(x => x.PageSize).PageSize(1, 500).When(x => x.PageSize.HasValue);
        RuleFor(x => x.Type).MaximumLength(64);
        RuleFor(x => x.Name).MaximumLength(256);
        RuleFor(x => x.TraceName).MaximumLength(256);
        RuleFor(x => x.TraceId).MaximumLength(128);
        RuleFor(x => x.AgentName).MaximumLength(128);
        RuleFor(x => x.Environment).MaximumLength(64);
        RuleFor(x => x.Level).MaximumLength(32);
        RuleFor(x => x.TimeRange).MaximumLength(32);
    }
}

// ── Mobile TTS ──────────────────────────────────────────────────────

internal sealed class MobileTextToSpeechSynthesisRequestValidator : Validator<MobileTextToSpeechSynthesisRequest>
{
    public MobileTextToSpeechSynthesisRequestValidator()
    {
        RuleFor(x => x.SpeechText)
            .NotEmpty().WithMessage("Speech text is required.")
            .MaximumLength(8_000).WithMessage("Speech text may not exceed 8,000 characters.");
        RuleFor(x => x.Locale).MaximumLength(16);
        RuleFor(x => x.ThreadId).MaximumLength(256);
        RuleFor(x => x.MessageId).MaximumLength(256);
    }
}

// ── Tenant Agent Settings ───────────────────────────────────────────

internal sealed class UpdateTenantAgentSettingsRequestValidator : Validator<UpdateTenantAgentSettingsRequest>
{
    public UpdateTenantAgentSettingsRequestValidator()
    {
        // KillSwitchEngaged is a nullable bool — no rule needed.
    }
}

// ── Capture-parse (Spec 047) ────────────────────────────────────────

internal sealed class CaptureParseRequestValidator : Validator<CaptureParseRequest>
{
    // ~6 MB image as base64 is ~8M chars; this is the request-edge backstop
    // (the service re-checks decoded bytes). Spec 047 §10/§12 — bounded payload.
    private const int MaxPayloadLength = 8_000_000;

    public CaptureParseRequestValidator()
    {
        RuleFor(x => x.InputType)
            .NotEmpty().WithMessage("inputType is required.")
            .Must(t => CaptureInputTypes.All.Contains(t))
            .WithMessage($"inputType must be one of: {string.Join(", ", CaptureInputTypes.All)}.");

        RuleFor(x => x.Payload)
            .NotEmpty().WithMessage("payload is required.")
            .MaximumLength(MaxPayloadLength).WithMessage("payload exceeds the maximum permitted size.");

        When(x => x.Hints is not null, () =>
        {
            RuleFor(x => x.Hints!.Entities)
                .Must(e => e is null || e.Count <= 500)
                .WithMessage("hints.entities may not exceed 500 items.");

            RuleFor(x => x.Hints!.OpenCommitments)
                .Must(c => c is null || c.Count <= 500)
                .WithMessage("hints.openCommitments may not exceed 500 items.");

            RuleForEach(x => x.Hints!.Entities).ChildRules(entity =>
            {
                entity.RuleFor(e => e.Id).RequiredText(128);
                entity.RuleFor(e => e.Name).RequiredText(256);
            });

            RuleForEach(x => x.Hints!.OpenCommitments).ChildRules(commitment =>
            {
                commitment.RuleFor(c => c.Id).RequiredText(128);
                commitment.RuleFor(c => c.Title).RequiredText(256);
                commitment.RuleFor(c => c.Expected!.Currency)
                    .CurrencyCode()
                    .When(c => c.Expected is not null);
            });
        });
    }
}

// ── Shared constants ────────────────────────────────────────────────

internal static class AiValidatorConstants
{
    internal static readonly string[] AllowedRiskTiers = ["Low", "Medium", "High", "Critical"];
    internal static readonly string[] AllowedDataSensitivities = ["Public", "Internal", "Confidential", "Restricted"];
}
