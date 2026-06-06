using Aonik.Agents.Contracts.Models.Tenant;
using Aonik.Agents.Services.Tenant;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Agents.Endpoints.TenantExtensions;

// Spec 033 §8.3 / §10 — tenant remote MCP server management + dry-run connect harness.

internal sealed class ListTenantMcpServersEndpoint : EndpointWithoutRequest<IReadOnlyList<TenantMcpServerDto>>
{
    private readonly ITenantMcpServerService _service;
    public ListTenantMcpServersEndpoint(ITenantMcpServerService service) => _service = service;

    public override void Configure()
    {
        Get("/ai/tenant-mcp-servers");
        Policies("AdminPolicy");
        Options(x => x.WithTags("AI Agent Extensions"));
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await _service.ListAsync(ct), ct);
}

internal sealed class GetTenantMcpServerEndpoint : EndpointWithoutRequest<TenantMcpServerDto>
{
    private readonly ITenantMcpServerService _service;
    public GetTenantMcpServerEndpoint(ITenantMcpServerService service) => _service = service;

    public override void Configure()
    {
        Get("/ai/tenant-mcp-servers/{Id}");
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

internal sealed class CreateTenantMcpServerEndpoint : Endpoint<SaveMcpServerRequest, TenantMcpServerDto>
{
    private readonly ITenantMcpServerService _service;
    public CreateTenantMcpServerEndpoint(ITenantMcpServerService service) => _service = service;

    public override void Configure()
    {
        Post("/ai/tenant-mcp-servers");
        Policies("AdminPolicy");
        Options(x => x.WithTags("AI Agent Extensions"));
    }

    public override async Task HandleAsync(SaveMcpServerRequest req, CancellationToken ct)
        => await Send.OkAsync(await _service.CreateAsync(req, ct), ct);
}

internal sealed class UpdateTenantMcpServerEndpoint : Endpoint<SaveMcpServerRequest, TenantMcpServerDto>
{
    private readonly ITenantMcpServerService _service;
    public UpdateTenantMcpServerEndpoint(ITenantMcpServerService service) => _service = service;

    public override void Configure()
    {
        Put("/ai/tenant-mcp-servers/{Id}");
        Policies("AdminPolicy");
        Options(x => x.WithTags("AI Agent Extensions"));
    }

    public override async Task HandleAsync(SaveMcpServerRequest req, CancellationToken ct)
    {
        var dto = await _service.UpdateAsync(Route<Guid>("Id"), req, ct);
        if (dto is null) { await Send.NotFoundAsync(ct); return; }
        await Send.OkAsync(dto, ct);
    }
}

internal sealed class DeleteTenantMcpServerEndpoint : EndpointWithoutRequest
{
    private readonly ITenantMcpServerService _service;
    public DeleteTenantMcpServerEndpoint(ITenantMcpServerService service) => _service = service;

    public override void Configure()
    {
        Delete("/ai/tenant-mcp-servers/{Id}");
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

internal sealed class SubmitTenantMcpServerEndpoint : TenantExtensionTransitionEndpoint<TenantMcpServerDto>
{
    private readonly ITenantMcpServerService _service;
    public SubmitTenantMcpServerEndpoint(ITenantMcpServerService service) => _service = service;

    public override void Configure()
    {
        Post("/ai/tenant-mcp-servers/{Id}/submit");
        Policies("AdminPolicy");
        Options(x => x.WithTags("AI Agent Extensions"));
    }

    public override Task HandleAsync(CancellationToken ct)
        => TransitionAsync(() => _service.SubmitAsync(Route<Guid>("Id"), ct), ct);
}

internal sealed class ActivateTenantMcpServerEndpoint : TenantExtensionTransitionEndpoint<TenantMcpServerDto>
{
    private readonly ITenantMcpServerService _service;
    public ActivateTenantMcpServerEndpoint(ITenantMcpServerService service) => _service = service;

    public override void Configure()
    {
        Post("/ai/tenant-mcp-servers/{Id}/activate");
        Policies("AdminPolicy");
        Options(x => x.WithTags("AI Agent Extensions"));
    }

    public override Task HandleAsync(CancellationToken ct)
        => TransitionAsync(() => _service.ActivateAsync(Route<Guid>("Id"), ct), ct);
}

internal sealed class DeactivateTenantMcpServerEndpoint : TenantExtensionTransitionEndpoint<TenantMcpServerDto>
{
    private readonly ITenantMcpServerService _service;
    public DeactivateTenantMcpServerEndpoint(ITenantMcpServerService service) => _service = service;

    public override void Configure()
    {
        Post("/ai/tenant-mcp-servers/{Id}/deactivate");
        Policies("AdminPolicy");
        Options(x => x.WithTags("AI Agent Extensions"));
    }

    public override Task HandleAsync(CancellationToken ct)
        => TransitionAsync(() => _service.DeactivateAsync(Route<Guid>("Id"), ct), ct);
}

internal sealed class TestTenantMcpServerEndpoint : EndpointWithoutRequest<McpDryRunDto>
{
    private readonly ITenantMcpServerService _service;
    public TestTenantMcpServerEndpoint(ITenantMcpServerService service) => _service = service;

    public override void Configure()
    {
        Post("/ai/tenant-mcp-servers/{Id}/test");
        Policies("AdminPolicy");
        Options(x => x.WithTags("AI Agent Extensions"));
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await _service.DryRunAsync(Route<Guid>("Id"), ct), ct);
}

internal sealed class ReviewTenantMcpServerEndpoint : TenantExtensionTransitionEndpoint<ReviewMcpServerRequest, TenantMcpServerDto>
{
    private readonly ITenantMcpServerService _service;
    public ReviewTenantMcpServerEndpoint(ITenantMcpServerService service) => _service = service;

    public override void Configure()
    {
        Post("/ai/tenant-mcp-servers/{Id}/review");
        Policies("PlatformAdmin");
        Options(x => x.WithTags("AI Agent Extensions"));
    }

    public override Task HandleAsync(ReviewMcpServerRequest req, CancellationToken ct)
        => TransitionAsync(() => _service.ReviewAsync(Route<Guid>("Id"), req, ct), ct);
}
