using Aonik.SharedKernel.Abstractions.Agents;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Agents.Framework;

/// <summary>
/// The Spec 033 §8.5 spine: the one <see cref="IToolApprovalManifest"/> that classifies every
/// tenant-contributed tool (remote MCP — §8.3, declarative HTTP — §8.4) so it is indistinguishable
/// from a built-in at the Spec 032 gate. Because tenant tools enter through the same
/// <c>GateAll</c> seam and the same manifest contract, the Spec 032 invariant — "no mutating agent
/// tool reaches a domain service without a recorded approval decision" — extends to tenant tools
/// for free. There is no second approval path for tenant extensions; they feed the one that
/// already exists and fails closed.
/// <para>
/// The gate is a singleton and <see cref="Classify"/> carries no tenant/scope context, so this
/// manifest reaches the request-scoped <see cref="ITenantToolClassificationStore"/> — populated by
/// the tenant tool providers as they build the current tenant's tools — via the active HTTP
/// request's services. When there is no HTTP request, or no store / no entry, it returns
/// <see langword="null"/>: the gate then applies its default rules, and a mutating-looking tenant
/// tool that somehow reached the gate unclassified throws <c>ToolNotClassifiedException</c> — fail
/// closed, exactly like a built-in.
/// </para>
/// </summary>
internal sealed class TenantToolApprovalManifest : IToolApprovalManifest
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantToolApprovalManifest(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string Module => "Tenant";

    public ToolClassification? Classify(string toolName)
    {
        // Tenant tools are built within the active request scope; that scope's classification store
        // holds what the providers registered for this tenant. Resolving via the request services
        // keeps the singleton gate and the scoped store on the same instance.
        var store = _httpContextAccessor.HttpContext?.RequestServices
            ?.GetService<ITenantToolClassificationStore>();

        return store?.Find(toolName);
    }
}
