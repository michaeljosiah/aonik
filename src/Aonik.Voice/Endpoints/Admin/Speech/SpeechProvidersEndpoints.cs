using Aonik.SharedKernel.Abstractions.Ai.Speech;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Voice.Endpoints.Admin.Speech;

// All endpoints under AdminPolicy + the existing CORS policy. Wire DTOs are wrapper records that
// mirror the service surface so we can evolve them independently of the service contract if/when
// versioning becomes a concern.

// ── List ────────────────────────────────────────────────────────────────────────────────

internal sealed class ListSpeechProvidersRequest
{
    [QueryParam] public SpeechProviderType? Type { get; set; }
    [QueryParam] public bool IncludeDisabled { get; set; }
}

internal sealed class ListSpeechProvidersEndpoint : Endpoint<ListSpeechProvidersRequest, IReadOnlyList<SpeechProvider>>
{
    private readonly ISpeechProviderLibraryService _service;
    public ListSpeechProvidersEndpoint(ISpeechProviderLibraryService service) => _service = service;

    public override void Configure()
    {
        Get("/tenant/speech-providers");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "List speech providers";
            s.Description = "Returns built-in archetypes plus tenant-owned providers. Filter by type, optionally include disabled rows.";
            s.Response(200, "Success");
            s.Response(401, "Not authenticated");
            s.Response(403, "Caller lacks AdminPolicy");
        });
        Options(x => x.WithTags("Speech library"));
    }

    public override async Task HandleAsync(ListSpeechProvidersRequest req, CancellationToken ct)
    {
        var result = await _service.ListAsync(req.Type, req.IncludeDisabled, ct);
        await Send.OkAsync(result, ct);
    }
}

// ── Get by id ───────────────────────────────────────────────────────────────────────────

internal sealed class GetSpeechProviderRequest
{
    public string Id { get; set; } = string.Empty;
}

internal sealed class GetSpeechProviderEndpoint : Endpoint<GetSpeechProviderRequest, SpeechProvider>
{
    private readonly ISpeechProviderLibraryService _service;
    public GetSpeechProviderEndpoint(ISpeechProviderLibraryService service) => _service = service;

    public override void Configure()
    {
        Get("/tenant/speech-providers/{id}");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Get speech provider";
            s.Description = "Resolve a provider by built-in id or tenant Guid.";
            s.Response(200, "Found");
            s.Response(404, "Not found");
        });
        Options(x => x.WithTags("Speech library"));
    }

    public override async Task HandleAsync(GetSpeechProviderRequest req, CancellationToken ct)
    {
        var result = await _service.GetAsync(req.Id, ct);
        if (result is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        await Send.OkAsync(result, ct);
    }
}

// ── Create ──────────────────────────────────────────────────────────────────────────────

internal sealed class CreateSpeechProviderEndpoint : Endpoint<CreateSpeechProviderRequest, SpeechProvider>
{
    private readonly ISpeechProviderLibraryService _service;
    public CreateSpeechProviderEndpoint(ISpeechProviderLibraryService service) => _service = service;

    public override void Configure()
    {
        Post("/tenant/speech-providers");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Create speech provider";
            s.Description = "Create a tenant-owned speech provider. Config payload type must match (Type, Vendor).";
            s.Response(200, "Created");
            s.Response(422, "Validation error — config shape does not match (Type, Vendor) or display name is invalid");
        });
        Options(x => x.WithTags("Speech library"));
    }

    public override async Task HandleAsync(CreateSpeechProviderRequest req, CancellationToken ct)
    {
        var result = await _service.CreateAsync(req, ct);
        await Send.OkAsync(result, ct);
    }
}

// ── Update ──────────────────────────────────────────────────────────────────────────────

/// <summary>Wire shape for PUT — route parameter <c>id</c> + body fields. Mirrors
/// <see cref="UpdateSpeechProviderRequest"/> field-for-field; we don't inherit because the
/// service contract is a sealed record.</summary>
internal sealed class UpdateSpeechProviderRouteRequest
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public SpeechProviderConfig Config { get; set; } = default!;
}

internal sealed class UpdateSpeechProviderEndpoint : Endpoint<UpdateSpeechProviderRouteRequest, SpeechProvider>
{
    private readonly ISpeechProviderLibraryService _service;
    public UpdateSpeechProviderEndpoint(ISpeechProviderLibraryService service) => _service = service;

    public override void Configure()
    {
        Put("/tenant/speech-providers/{id}");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Update speech provider";
            s.Description = "Update display name and/or config of a tenant-owned provider. Bumps Version and archives the previous snapshot.";
            s.Response(200, "Updated");
            s.Response(409, "Built-in archetypes are immutable — clone first");
            s.Response(422, "Validation error");
        });
        Options(x => x.WithTags("Speech library"));
    }

    public override async Task HandleAsync(UpdateSpeechProviderRouteRequest req, CancellationToken ct)
    {
        var inner = new UpdateSpeechProviderRequest(DisplayName: req.DisplayName, Config: req.Config);
        var result = await _service.UpdateAsync(req.Id, inner, ct);
        await Send.OkAsync(result, ct);
    }
}

// ── Clone built-in ──────────────────────────────────────────────────────────────────────

internal sealed class CloneSpeechProviderRequest
{
    /// <summary>Built-in id (e.g. <c>built-in:openai-tts-alloy</c>).</summary>
    public string Id { get; set; } = string.Empty;
    /// <summary>Optional new display name. Defaults to <c>"{archetype name} (copy)"</c>.</summary>
    public string? NewDisplayName { get; set; }
}

internal sealed class CloneSpeechProviderEndpoint : Endpoint<CloneSpeechProviderRequest, SpeechProvider>
{
    private readonly ISpeechProviderLibraryService _service;
    public CloneSpeechProviderEndpoint(ISpeechProviderLibraryService service) => _service = service;

    public override void Configure()
    {
        Post("/tenant/speech-providers/{id}/clone");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Clone built-in archetype";
            s.Description = "Create a tenant-owned editable copy of a built-in archetype.";
            s.Response(200, "Cloned");
            s.Response(422, "Built-in id does not exist");
        });
        Options(x => x.WithTags("Speech library"));
    }

    public override async Task HandleAsync(CloneSpeechProviderRequest req, CancellationToken ct)
    {
        var result = await _service.CloneBuiltInAsync(req.Id, req.NewDisplayName, ct);
        await Send.OkAsync(result, ct);
    }
}

// ── Status ──────────────────────────────────────────────────────────────────────────────

internal sealed class SetSpeechProviderStatusRequest
{
    public Guid Id { get; set; }
    public SpeechProviderStatus Status { get; set; }
}

internal sealed class SetSpeechProviderStatusEndpoint : Endpoint<SetSpeechProviderStatusRequest, SpeechProvider>
{
    private readonly ISpeechProviderLibraryService _service;
    public SetSpeechProviderStatusEndpoint(ISpeechProviderLibraryService service) => _service = service;

    public override void Configure()
    {
        Put("/tenant/speech-providers/{id}/status");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Change speech provider status";
            s.Description = "Toggle between Active / Disabled / SoftDeleted. Disabling or soft-deleting a provider that is referenced by an active recipe returns 409.";
            s.Response(200, "Updated");
            s.Response(409, "Provider is in use by an active recipe");
            s.Response(422, "Provider not found");
        });
        Options(x => x.WithTags("Speech library"));
    }

    public override async Task HandleAsync(SetSpeechProviderStatusRequest req, CancellationToken ct)
    {
        var result = await _service.SetStatusAsync(req.Id, req.Status, ct);
        await Send.OkAsync(result, ct);
    }
}

// ── History ─────────────────────────────────────────────────────────────────────────────

internal sealed class GetSpeechProviderHistoryEndpoint : Endpoint<GetSpeechProviderRequest, IReadOnlyList<SpeechProviderHistoryEntry>>
{
    private readonly ISpeechProviderLibraryService _service;
    public GetSpeechProviderHistoryEndpoint(ISpeechProviderLibraryService service) => _service = service;

    public override void Configure()
    {
        Get("/tenant/speech-providers/{id}/history");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Get speech provider edit history";
            s.Description = "Returns the most recent N snapshots in newest-first order. Built-in archetypes have no history.";
            s.Response(200, "History list (possibly empty)");
        });
        Options(x => x.WithTags("Speech library"));
    }

    public override async Task HandleAsync(GetSpeechProviderRequest req, CancellationToken ct)
    {
        var result = await _service.GetHistoryAsync(req.Id, ct);
        await Send.OkAsync(result, ct);
    }
}

// ── Usage ───────────────────────────────────────────────────────────────────────────────

internal sealed class GetSpeechProviderUsageEndpoint : Endpoint<GetSpeechProviderRequest, SpeechProviderUsage>
{
    private readonly ISpeechProviderLibraryService _service;
    public GetSpeechProviderUsageEndpoint(ISpeechProviderLibraryService service) => _service = service;

    public override void Configure()
    {
        Get("/tenant/speech-providers/{id}/usage");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Get speech provider usage";
            s.Description = "Returns the recipes that reference this provider. Returns an empty list in Phase A; populated in Phase B once the recipe library lands.";
            s.Response(200, "Usage report");
        });
        Options(x => x.WithTags("Speech library"));
    }

    public override async Task HandleAsync(GetSpeechProviderRequest req, CancellationToken ct)
    {
        var result = await _service.GetUsageAsync(req.Id, ct);
        await Send.OkAsync(result, ct);
    }
}
