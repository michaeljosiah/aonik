using System.Reflection;

using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Modules;

using FastEndpoints;

namespace Aonik.Api.Middleware;

/// <summary>
/// The per-tenant module gate for HTTP (Spec 097 §11). Denies any request whose endpoint belongs to
/// a module the current tenant has switched off by throwing <see cref="ModuleDisabledException"/>,
/// which <c>ExceptionHandlerConfiguration</c> maps to <c>403 { error, code: "module.disabled", moduleId }</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why middleware and not a FastEndpoints pre-processor.</b> <c>Program.cs</c> also maps
/// minimal-API and WebSocket endpoints straight from module assemblies (the Voice WebSocket, the
/// playground and notification-stream maps). A FastEndpoints pre-processor never sees those, so a
/// tenant with Voice off would still get a live voice socket. Routing has already resolved the
/// endpoint by the time this middleware runs, so both kinds are visible through
/// <see cref="EndpointHttpContextExtensions.GetEndpoint(HttpContext)"/> and one gate covers every
/// endpoint with no per-endpoint opt-in.
/// </para>
/// <para>
/// <b>Why the gate keys on the assembly.</b> Route prefixes are not a module boundary here: Platform
/// serves <c>/personal-finance/*</c>, Finance serves <c>/admin/*</c> and <c>/public/*</c>, and
/// PersonalFinance serves <c>/admin/*</c> too. The endpoint type's assembly carries an
/// <see cref="AonikModuleAttribute"/>, which is the one fact that is always right and can never be
/// forgotten when a new endpoint is added. Per-endpoint attributes would reintroduce the
/// "unclassified" failure mode that Spec 032 had to close with a build-time exception.
/// </para>
/// <para>
/// <b>Ordering constraint.</b> Register with <see cref="ModuleEnablementMiddlewareExtensions.UseModuleEnablement"/>
/// immediately after <c>UseTenantValidation()</c> and before <c>UseFastEndpoints(...)</c>. That places
/// it after authentication (so an anonymous caller is rejected by the policy, not by a module
/// message), after tenant-context resolution (so <see cref="ITenantProvider"/> answers), after
/// authorization, and inside the exception handler that owns the 403 mapping. Putting it before
/// <c>UseRouting()</c> would leave <c>GetEndpoint()</c> null and silently disable the gate.
/// </para>
/// <para>
/// <b>Resolution order</b> (each step either decides or falls through to the pipeline):
/// <list type="number">
///   <item>No routed endpoint: continue.</item>
///   <item>Owning type from the endpoint metadata — <see cref="EndpointDefinition.EndpointType"/> for
///   FastEndpoints, <see cref="MethodInfo.DeclaringType"/> for minimal APIs, and the declaring type of
///   <see cref="Endpoint.RequestDelegate"/> for endpoints mapped straight from a
///   <see cref="RequestDelegate"/> method group (the Voice WebSocket, the notification stream), which
///   carry no <see cref="MethodInfo"/> metadata at all. No type, or an assembly with no
///   <see cref="AonikModuleAttribute"/> (Api-hosted, Application, Infrastructure): continue.</item>
///   <item>Core module: continue. Core modules can never be off.</item>
///   <item><see cref="ModuleGateExempt"/> metadata on the endpoint: continue. A greppable, reviewable opt-out.</item>
///   <item>No resolved tenant: continue. There is nothing to gate against; the tenant middleware already
///   decided who may be anonymous. Storefront endpoints that resolve the tenant anonymously by header
///   or subdomain DO reach the next step, which is intended for a shop whose Commerce module is off.
///   This pass-through is deliberate and is NOT the end of enforcement: a pre-tenant request that
///   resolves its tenant later — an anonymous provider callback that finds the owning tenant on the
///   payout or connection it references, the sandbox tool callback that finds it in a signed nonce —
///   re-checks through <see cref="IModuleGate"/> the moment that tenant is known, before it mutates
///   anything. The middleware cannot do that for it, because the tenant does not exist yet when the
///   middleware runs.</item>
///   <item>Reader says disabled: throw <see cref="ModuleDisabledException"/>. Otherwise run the rest of
///   the pipeline inside a log scope carrying <c>ModuleId</c> and <c>TenantId</c>, so a denied or
///   slow call is one query away in observability.</item>
/// </list>
/// Cost is one cached read per request, shared with the manifest and the agent resolver through the
/// reader's scoped memo. Scoped services are resolved from <see cref="HttpContext.RequestServices"/>
/// inside <see cref="InvokeAsync"/>, never from the constructor, because middleware is a singleton.
/// </para>
/// </remarks>
public sealed class ModuleEnablementMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ModuleEnablementMiddleware> _logger;

    public ModuleEnablementMiddleware(RequestDelegate next, ILogger<ModuleEnablementMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 1. Nothing routed (404 territory, static files, unmatched OPTIONS) — nothing to gate.
        var endpoint = context.GetEndpoint();
        if (endpoint is null)
        {
            await _next(context);
            return;
        }

        // 2. Owning type → assembly → module id. Api/Platform-hosted and unattributed types fall through.
        var moduleId = ResolveModuleId(endpoint);
        if (moduleId is null)
        {
            await _next(context);
            return;
        }

        // 3. Core modules can never be off. An id the catalogue does not know is a build defect that the
        //    P1 assembly-enumeration test catches; it is not this request's problem, so fall through.
        var descriptor = ModuleCatalog.TryGet(moduleId);
        if (descriptor is null || descriptor.IsCore)
        {
            await _next(context);
            return;
        }

        // 4. Explicit, greppable exemption.
        if (endpoint.Metadata.GetMetadata<ModuleGateExempt>() is not null)
        {
            await _next(context);
            return;
        }

        // 5. No tenant, no gate. The tenant middleware owns the anonymous decision. Callback processors that
        //    resolve their tenant later re-check through IModuleGate (see the remarks) — this branch is a
        //    pass-through for pre-tenant requests, not an exemption.
        var tenantProvider = context.RequestServices.GetRequiredService<ITenantProvider>();
        if (!tenantProvider.TryGetCurrentTenantId(out var tenantId))
        {
            await _next(context);
            return;
        }

        // 6. One cached, dependency-consistent read; deny or proceed under a module log scope.
        var reader = context.RequestServices.GetRequiredService<IModuleEnablementReader>();
        var enablement = await reader.GetAsync(tenantId, context.RequestAborted);

        if (!enablement.IsEnabled(moduleId))
        {
            _logger.LogInformation(
                "Module {ModuleId} is disabled for tenant {TenantId}; denying {Method} {Path}.",
                moduleId, tenantId, context.Request.Method, context.Request.Path);

            throw new ModuleDisabledException(moduleId);
        }

        using (_logger.BeginScope(new Dictionary<string, object>
               {
                   ["ModuleId"] = moduleId,
                   ["TenantId"] = tenantId,
               }))
        {
            await _next(context);
        }
    }

    /// <summary>
    /// The catalogue id of the module whose assembly declares <paramref name="endpoint"/>'s handler,
    /// or null when the endpoint has no resolvable owning type or that type's assembly carries no
    /// <see cref="AonikModuleAttribute"/>.
    /// </summary>
    public static string? ResolveModuleId(Endpoint endpoint)
    {
        var owningType = ResolveOwningType(endpoint);
        return owningType is null ? null : ModuleCatalog.TryGetModuleId(owningType);
    }

    /// <summary>
    /// FastEndpoints attaches its <see cref="EndpointDefinition"/> to the routed endpoint's metadata;
    /// minimal APIs mapped through the <see cref="Delegate"/> overloads attach the handler's
    /// <see cref="MethodInfo"/>. Either yields the declaring type. An endpoint mapped through the
    /// <see cref="RequestDelegate"/> overload (<c>MapGet(pattern, VoiceWebSocketEndpoint.HandleAsync)</c>)
    /// gets neither, so the third source is the delegate itself: its target method's declaring type,
    /// walked outward through any compiler-generated closure or display class to the outermost type,
    /// because only the assembly matters for the module id.
    /// </summary>
    public static Type? ResolveOwningType(Endpoint endpoint)
    {
        var definition = endpoint.Metadata.GetMetadata<EndpointDefinition>();
        if (definition is not null)
        {
            return definition.EndpointType;
        }

        var handler = endpoint.Metadata.GetMetadata<MethodInfo>();
        if (handler?.DeclaringType is not null)
        {
            return handler.DeclaringType;
        }

        return OutermostType(endpoint.RequestDelegate?.Method.DeclaringType);
    }

    private static Type? OutermostType(Type? type)
    {
        while (type?.DeclaringType is not null)
        {
            type = type.DeclaringType;
        }

        return type;
    }
}

public static class ModuleEnablementMiddlewareExtensions
{
    /// <summary>
    /// Adds the per-tenant module gate (Spec 097 §11). Must run after routing, authentication, tenant
    /// context and authorization, and before FastEndpoints — see the <see cref="ModuleEnablementMiddleware"/>
    /// remarks for why.
    /// </summary>
    public static IApplicationBuilder UseModuleEnablement(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ModuleEnablementMiddleware>();
    }
}
