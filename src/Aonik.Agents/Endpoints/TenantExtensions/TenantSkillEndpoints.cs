using Aonik.Agents.Contracts.Models.Tenant;
using Aonik.Agents.Services.Tenant;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Agents.Endpoints.TenantExtensions;

// Spec 033 §10 — tenant skill management + the "validate skill" harness. TenantAdmin manages and
// activates (AdminPolicy = PlatformAdmin or TenantAdmin); PlatformAdmin reviews and enables scripts.

public sealed class ReviewTenantSkillRequest
{
    public Guid Id { get; init; }
    public bool Approve { get; init; }
    public string? Notes { get; init; }
}

public sealed class EnableTenantSkillScriptsRequestBody
{
    public Guid Id { get; init; }
    public bool Enabled { get; init; }
    public string? Notes { get; init; }
}

internal sealed class ListTenantSkillsEndpoint : EndpointWithoutRequest<IReadOnlyList<TenantSkillDto>>
{
    private readonly ITenantSkillService _service;
    public ListTenantSkillsEndpoint(ITenantSkillService service) => _service = service;

    public override void Configure()
    {
        Get("/ai/tenant-skills");
        Policies("AdminPolicy");
        Options(x => x.WithTags("AI Agent Extensions"));
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await _service.ListAsync(ct), ct);
}

internal sealed class ValidateTenantSkillEndpoint : Endpoint<ValidateSkillRequest, SkillValidationDto>
{
    private readonly ITenantSkillService _service;
    public ValidateTenantSkillEndpoint(ITenantSkillService service) => _service = service;

    public override void Configure()
    {
        Post("/ai/tenant-skills/validate");
        Policies("AdminPolicy");
        Options(x => x.WithTags("AI Agent Extensions"));
    }

    public override async Task HandleAsync(ValidateSkillRequest req, CancellationToken ct)
        => await Send.OkAsync(await _service.ValidateAsync(req.Markdown ?? string.Empty, ct), ct);
}

internal sealed class UploadTenantSkillEndpoint : Endpoint<UploadSkillRequest, TenantSkillDto>
{
    private readonly ITenantSkillService _service;
    public UploadTenantSkillEndpoint(ITenantSkillService service) => _service = service;

    public override void Configure()
    {
        Post("/ai/tenant-skills");
        Policies("AdminPolicy");
        Options(x => x.WithTags("AI Agent Extensions"));
    }

    public override async Task HandleAsync(UploadSkillRequest req, CancellationToken ct)
    {
        var (dto, validation) = await _service.UploadAsync(req.Markdown ?? string.Empty, ct);
        if (dto is null)
        {
            foreach (var error in validation.Errors)
            {
                AddError(error);
            }
            await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);
            return;
        }
        await Send.OkAsync(dto, ct);
    }
}

internal sealed class PreviewTenantSkillEndpoint : EndpointWithoutRequest<SkillPreviewDto>
{
    private readonly ITenantSkillService _service;
    public PreviewTenantSkillEndpoint(ITenantSkillService service) => _service = service;

    public override void Configure()
    {
        Get("/ai/tenant-skills/{Id}/preview");
        Policies("AdminPolicy");
        Options(x => x.WithTags("AI Agent Extensions"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var dto = await _service.PreviewAsync(Route<Guid>("Id"), ct);
        if (dto is null) { await Send.NotFoundAsync(ct); return; }
        await Send.OkAsync(dto, ct);
    }
}

internal sealed class SubmitTenantSkillEndpoint : TenantExtensionTransitionEndpoint<TenantSkillDto>
{
    private readonly ITenantSkillService _service;
    public SubmitTenantSkillEndpoint(ITenantSkillService service) => _service = service;

    public override void Configure()
    {
        Post("/ai/tenant-skills/{Id}/submit");
        Policies("AdminPolicy");
        Options(x => x.WithTags("AI Agent Extensions"));
    }

    public override Task HandleAsync(CancellationToken ct)
        => TransitionAsync(() => _service.SubmitAsync(Route<Guid>("Id"), ct), ct);
}

internal sealed class ActivateTenantSkillEndpoint : TenantExtensionTransitionEndpoint<TenantSkillDto>
{
    private readonly ITenantSkillService _service;
    public ActivateTenantSkillEndpoint(ITenantSkillService service) => _service = service;

    public override void Configure()
    {
        Post("/ai/tenant-skills/{Id}/activate");
        Policies("AdminPolicy");
        Options(x => x.WithTags("AI Agent Extensions"));
    }

    public override Task HandleAsync(CancellationToken ct)
        => TransitionAsync(() => _service.ActivateAsync(Route<Guid>("Id"), ct), ct);
}

internal sealed class DeactivateTenantSkillEndpoint : TenantExtensionTransitionEndpoint<TenantSkillDto>
{
    private readonly ITenantSkillService _service;
    public DeactivateTenantSkillEndpoint(ITenantSkillService service) => _service = service;

    public override void Configure()
    {
        Post("/ai/tenant-skills/{Id}/deactivate");
        Policies("AdminPolicy");
        Options(x => x.WithTags("AI Agent Extensions"));
    }

    public override Task HandleAsync(CancellationToken ct)
        => TransitionAsync(() => _service.DeactivateAsync(Route<Guid>("Id"), ct), ct);
}

internal sealed class DeleteTenantSkillEndpoint : EndpointWithoutRequest
{
    private readonly ITenantSkillService _service;
    public DeleteTenantSkillEndpoint(ITenantSkillService service) => _service = service;

    public override void Configure()
    {
        Delete("/ai/tenant-skills/{Id}");
        Policies("AdminPolicy");
        Options(x => x.WithTags("AI Agent Extensions"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!await _service.DeleteAsync(Route<Guid>("Id"), ct))
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        await Send.NoContentAsync(ct);
    }
}

internal sealed class ReviewTenantSkillEndpoint : TenantExtensionTransitionEndpoint<ReviewTenantSkillRequest, TenantSkillDto>
{
    private readonly ITenantSkillService _service;
    public ReviewTenantSkillEndpoint(ITenantSkillService service) => _service = service;

    public override void Configure()
    {
        Post("/ai/tenant-skills/{Id}/review");
        Policies("PlatformAdmin");
        Options(x => x.WithTags("AI Agent Extensions"));
    }

    public override Task HandleAsync(ReviewTenantSkillRequest req, CancellationToken ct)
        => TransitionAsync(() => _service.ReviewAsync(req.Id, req.Approve, req.Notes, ct), ct);
}

internal sealed class EnableTenantSkillScriptsEndpoint : TenantExtensionTransitionEndpoint<EnableTenantSkillScriptsRequestBody, TenantSkillDto>
{
    private readonly ITenantSkillService _service;
    public EnableTenantSkillScriptsEndpoint(ITenantSkillService service) => _service = service;

    public override void Configure()
    {
        Post("/ai/tenant-skills/{Id}/enable-scripts");
        Policies("PlatformAdmin");
        Options(x => x.WithTags("AI Agent Extensions"));
    }

    public override Task HandleAsync(EnableTenantSkillScriptsRequestBody req, CancellationToken ct)
        => TransitionAsync(() => _service.EnableScriptsAsync(req.Id, req.Enabled, req.Notes, ct), ct);
}
