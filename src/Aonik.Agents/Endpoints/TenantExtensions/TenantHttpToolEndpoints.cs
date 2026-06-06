using Aonik.Agents.Contracts.Models.Tenant;
using Aonik.Agents.Services.Tenant;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Agents.Endpoints.TenantExtensions;

// Spec 033 §8.4 / §10 — tenant declarative HTTP/OpenAPI tool management + classification harness.

internal sealed class ListTenantHttpToolsEndpoint : EndpointWithoutRequest<IReadOnlyList<TenantHttpToolDto>>
{
    private readonly ITenantHttpToolService _service;
    public ListTenantHttpToolsEndpoint(ITenantHttpToolService service) => _service = service;

    public override void Configure()
    {
        Get("/ai/tenant-http-tools");
        Policies("AdminPolicy");
        Options(x => x.WithTags("AI Agent Extensions"));
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await _service.ListAsync(ct), ct);
}

internal sealed class GetTenantHttpToolEndpoint : EndpointWithoutRequest<TenantHttpToolDto>
{
    private readonly ITenantHttpToolService _service;
    public GetTenantHttpToolEndpoint(ITenantHttpToolService service) => _service = service;

    public override void Configure()
    {
        Get("/ai/tenant-http-tools/{Id}");
        Policies("AdminPolicy");
        Options(x => x.WithTags("AI Agent Extensions"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var dto = await _service.GetAsync(Route<Guid>("Id"), ct);
        if (dto is null) { await Send.NotFoundAsync(ct); return; }
        await Send.OkAsync(dto, ct);
    }
}

internal sealed class CreateTenantHttpToolEndpoint : Endpoint<SaveHttpToolRequest, TenantHttpToolDto>
{
    private readonly ITenantHttpToolService _service;
    public CreateTenantHttpToolEndpoint(ITenantHttpToolService service) => _service = service;

    public override void Configure()
    {
        Post("/ai/tenant-http-tools");
        Policies("AdminPolicy");
        Options(x => x.WithTags("AI Agent Extensions"));
    }

    public override async Task HandleAsync(SaveHttpToolRequest req, CancellationToken ct)
        => await Send.OkAsync(await _service.CreateAsync(req, ct), ct);
}

internal sealed class UpdateTenantHttpToolEndpoint : Endpoint<SaveHttpToolRequest, TenantHttpToolDto>
{
    private readonly ITenantHttpToolService _service;
    public UpdateTenantHttpToolEndpoint(ITenantHttpToolService service) => _service = service;

    public override void Configure()
    {
        Put("/ai/tenant-http-tools/{Id}");
        Policies("AdminPolicy");
        Options(x => x.WithTags("AI Agent Extensions"));
    }

    public override async Task HandleAsync(SaveHttpToolRequest req, CancellationToken ct)
    {
        var dto = await _service.UpdateAsync(Route<Guid>("Id"), req, ct);
        if (dto is null) { await Send.NotFoundAsync(ct); return; }
        await Send.OkAsync(dto, ct);
    }
}

internal sealed class DeleteTenantHttpToolEndpoint : EndpointWithoutRequest
{
    private readonly ITenantHttpToolService _service;
    public DeleteTenantHttpToolEndpoint(ITenantHttpToolService service) => _service = service;

    public override void Configure()
    {
        Delete("/ai/tenant-http-tools/{Id}");
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

internal sealed class SubmitTenantHttpToolEndpoint : TenantExtensionTransitionEndpoint<TenantHttpToolDto>
{
    private readonly ITenantHttpToolService _service;
    public SubmitTenantHttpToolEndpoint(ITenantHttpToolService service) => _service = service;

    public override void Configure()
    {
        Post("/ai/tenant-http-tools/{Id}/submit");
        Policies("AdminPolicy");
        Options(x => x.WithTags("AI Agent Extensions"));
    }

    public override Task HandleAsync(CancellationToken ct)
        => TransitionAsync(() => _service.SubmitAsync(Route<Guid>("Id"), ct), ct);
}

internal sealed class ActivateTenantHttpToolEndpoint : TenantExtensionTransitionEndpoint<TenantHttpToolDto>
{
    private readonly ITenantHttpToolService _service;
    public ActivateTenantHttpToolEndpoint(ITenantHttpToolService service) => _service = service;

    public override void Configure()
    {
        Post("/ai/tenant-http-tools/{Id}/activate");
        Policies("AdminPolicy");
        Options(x => x.WithTags("AI Agent Extensions"));
    }

    public override Task HandleAsync(CancellationToken ct)
        => TransitionAsync(() => _service.ActivateAsync(Route<Guid>("Id"), ct), ct);
}

internal sealed class DeactivateTenantHttpToolEndpoint : TenantExtensionTransitionEndpoint<TenantHttpToolDto>
{
    private readonly ITenantHttpToolService _service;
    public DeactivateTenantHttpToolEndpoint(ITenantHttpToolService service) => _service = service;

    public override void Configure()
    {
        Post("/ai/tenant-http-tools/{Id}/deactivate");
        Policies("AdminPolicy");
        Options(x => x.WithTags("AI Agent Extensions"));
    }

    public override Task HandleAsync(CancellationToken ct)
        => TransitionAsync(() => _service.DeactivateAsync(Route<Guid>("Id"), ct), ct);
}

internal sealed class TestTenantHttpToolEndpoint : EndpointWithoutRequest<HttpToolTestDto>
{
    private readonly ITenantHttpToolService _service;
    public TestTenantHttpToolEndpoint(ITenantHttpToolService service) => _service = service;

    public override void Configure()
    {
        Post("/ai/tenant-http-tools/{Id}/test");
        Policies("AdminPolicy");
        Options(x => x.WithTags("AI Agent Extensions"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var dto = await _service.TestAsync(Route<Guid>("Id"), ct);
        if (dto is null) { await Send.NotFoundAsync(ct); return; }
        await Send.OkAsync(dto, ct);
    }
}

internal sealed class ReviewTenantHttpToolEndpoint : TenantExtensionTransitionEndpoint<ReviewHttpToolRequest, TenantHttpToolDto>
{
    private readonly ITenantHttpToolService _service;
    public ReviewTenantHttpToolEndpoint(ITenantHttpToolService service) => _service = service;

    public override void Configure()
    {
        Post("/ai/tenant-http-tools/{Id}/review");
        Policies("PlatformAdmin");
        Options(x => x.WithTags("AI Agent Extensions"));
    }

    public override Task HandleAsync(ReviewHttpToolRequest req, CancellationToken ct)
        => TransitionAsync(() => _service.ReviewAsync(Route<Guid>("Id"), req, ct), ct);
}
