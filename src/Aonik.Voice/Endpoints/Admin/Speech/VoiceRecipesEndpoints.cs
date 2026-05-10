using Aonik.SharedKernel.Abstractions.Ai.Speech;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Voice.Endpoints.Admin.Speech;

// Recipes admin endpoints. Same shape as the providers endpoints (CRUD + clone + status +
// history) under AdminPolicy. Test endpoint is intentionally NOT defined for recipes — a
// recipe-level test requires the full WS round-trip (Phase C live pipeline test card).

// ── List ────────────────────────────────────────────────────────────────────────────────

internal sealed class ListVoiceRecipesRequest
{
    [QueryParam] public VoiceRecipeKind? Kind { get; set; }
    [QueryParam] public bool IncludeDisabled { get; set; }
}

internal sealed class ListVoiceRecipesEndpoint : Endpoint<ListVoiceRecipesRequest, IReadOnlyList<VoiceRecipe>>
{
    private readonly IVoiceRecipeLibraryService _service;
    public ListVoiceRecipesEndpoint(IVoiceRecipeLibraryService service) => _service = service;

    public override void Configure()
    {
        Get("/tenant/voice-recipes");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "List voice recipes";
            s.Description = "Built-in recipes plus tenant-owned. Filter by kind, optionally include disabled.";
            s.Response(200, "Success");
        });
        Options(x => x.WithTags("Speech library"));
    }

    public override async Task HandleAsync(ListVoiceRecipesRequest req, CancellationToken ct)
    {
        var result = await _service.ListAsync(req.Kind, req.IncludeDisabled, ct);
        await Send.OkAsync(result, ct);
    }
}

// ── Get ─────────────────────────────────────────────────────────────────────────────────

internal sealed class GetVoiceRecipeRequest
{
    public string Id { get; set; } = string.Empty;
}

internal sealed class GetVoiceRecipeEndpoint : Endpoint<GetVoiceRecipeRequest, VoiceRecipe>
{
    private readonly IVoiceRecipeLibraryService _service;
    public GetVoiceRecipeEndpoint(IVoiceRecipeLibraryService service) => _service = service;

    public override void Configure()
    {
        Get("/tenant/voice-recipes/{id}");
        Policies("AdminPolicy");
        Options(x => x.WithTags("Speech library"));
    }

    public override async Task HandleAsync(GetVoiceRecipeRequest req, CancellationToken ct)
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

internal sealed class CreateVoiceRecipeEndpoint : Endpoint<CreateVoiceRecipeRequest, VoiceRecipe>
{
    private readonly IVoiceRecipeLibraryService _service;
    public CreateVoiceRecipeEndpoint(IVoiceRecipeLibraryService service) => _service = service;

    public override void Configure()
    {
        Post("/tenant/voice-recipes");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Create voice recipe";
            s.Description = "Create a tenant-owned recipe. Validates that referenced provider ids resolve to providers of the right type.";
            s.Response(200, "Created");
            s.Response(422, "Validation error");
        });
        Options(x => x.WithTags("Speech library"));
    }

    public override async Task HandleAsync(CreateVoiceRecipeRequest req, CancellationToken ct)
    {
        var result = await _service.CreateAsync(req, ct);
        await Send.OkAsync(result, ct);
    }
}

// ── Update ──────────────────────────────────────────────────────────────────────────────

internal sealed class UpdateVoiceRecipeRouteRequest
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ChainedRecipeBody? Chained { get; set; }
    public CompositeRecipeBody? Composite { get; set; }
}

internal sealed class UpdateVoiceRecipeEndpoint : Endpoint<UpdateVoiceRecipeRouteRequest, VoiceRecipe>
{
    private readonly IVoiceRecipeLibraryService _service;
    public UpdateVoiceRecipeEndpoint(IVoiceRecipeLibraryService service) => _service = service;

    public override void Configure()
    {
        Put("/tenant/voice-recipes/{id}");
        Policies("AdminPolicy");
        Options(x => x.WithTags("Speech library"));
    }

    public override async Task HandleAsync(UpdateVoiceRecipeRouteRequest req, CancellationToken ct)
    {
        var inner = new UpdateVoiceRecipeRequest(req.DisplayName, req.Description, req.Chained, req.Composite);
        var result = await _service.UpdateAsync(req.Id, inner, ct);
        await Send.OkAsync(result, ct);
    }
}

// ── Clone ───────────────────────────────────────────────────────────────────────────────

internal sealed class CloneVoiceRecipeRequest
{
    public string Id { get; set; } = string.Empty;
    public string? NewDisplayName { get; set; }
}

internal sealed class CloneVoiceRecipeEndpoint : Endpoint<CloneVoiceRecipeRequest, VoiceRecipe>
{
    private readonly IVoiceRecipeLibraryService _service;
    public CloneVoiceRecipeEndpoint(IVoiceRecipeLibraryService service) => _service = service;

    public override void Configure()
    {
        Post("/tenant/voice-recipes/{id}/clone");
        Policies("AdminPolicy");
        Options(x => x.WithTags("Speech library"));
    }

    public override async Task HandleAsync(CloneVoiceRecipeRequest req, CancellationToken ct)
    {
        var result = await _service.CloneBuiltInAsync(req.Id, req.NewDisplayName, ct);
        await Send.OkAsync(result, ct);
    }
}

// ── Status ──────────────────────────────────────────────────────────────────────────────

internal sealed class SetVoiceRecipeStatusRequest
{
    public Guid Id { get; set; }
    public VoiceRecipeStatus Status { get; set; }
}

internal sealed class SetVoiceRecipeStatusEndpoint : Endpoint<SetVoiceRecipeStatusRequest, VoiceRecipe>
{
    private readonly IVoiceRecipeLibraryService _service;
    public SetVoiceRecipeStatusEndpoint(IVoiceRecipeLibraryService service) => _service = service;

    public override void Configure()
    {
        Put("/tenant/voice-recipes/{id}/status");
        Policies("AdminPolicy");
        Options(x => x.WithTags("Speech library"));
    }

    public override async Task HandleAsync(SetVoiceRecipeStatusRequest req, CancellationToken ct)
    {
        var result = await _service.SetStatusAsync(req.Id, req.Status, ct);
        await Send.OkAsync(result, ct);
    }
}

// ── History ─────────────────────────────────────────────────────────────────────────────

internal sealed class GetVoiceRecipeHistoryEndpoint : Endpoint<GetVoiceRecipeRequest, IReadOnlyList<VoiceRecipeHistoryEntry>>
{
    private readonly IVoiceRecipeLibraryService _service;
    public GetVoiceRecipeHistoryEndpoint(IVoiceRecipeLibraryService service) => _service = service;

    public override void Configure()
    {
        Get("/tenant/voice-recipes/{id}/history");
        Policies("AdminPolicy");
        Options(x => x.WithTags("Speech library"));
    }

    public override async Task HandleAsync(GetVoiceRecipeRequest req, CancellationToken ct)
    {
        var result = await _service.GetHistoryAsync(req.Id, ct);
        await Send.OkAsync(result, ct);
    }
}
