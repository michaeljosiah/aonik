using Aonik.Commerce.Services.Checkout;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Aonik.Commerce.Services.Catalog;
using Aonik.Platform.Contracts.Services.Modules;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ai.Speech;
using Aonik.SharedKernel.Modules;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Api.Configuration;

/// <summary>
/// Global exception handler middleware for the Aonik API.
/// </summary>
/// <remarks>
/// Extracted out of the previous custom CORS middleware (which mixed
/// CORS, preflight, and exception-handling concerns). The standard
/// <c>UseCors</c> now owns CORS; this middleware owns:
/// <list type="bullet">
///   <item>Logging unhandled exceptions with their full inner-chain so the
///         App Insights / Logs page surfaces root-cause SQL / network
///         errors without forcing the operator to expand the formatted
///         exception block.</item>
///   <item>Stamping the active OTel span with <c>error</c> tags so the
///         trace explorer renders the failure inline on the request span.</item>
///   <item>Mapping <see cref="PermissionDeniedException"/> to a 403 with
///         the missing <c>permissionKey</c> in the response body — kept
///         distinct from a 500 so the front-end can render an authorisation
///         problem with the right tone.</item>
///   <item>Classifying <em>expected policy responses</em> — a module the
///         tenant switched off (<see cref="ModuleDisabledException"/>, 403), a
///         toggle that would break the dependency graph
///         (<see cref="ModuleDependencyException"/>, 409), a permission the
///         caller lacks (<see cref="PermissionDeniedException"/>, 403) —
///         <b>before</b> the unhandled-exception logging and span stamping.
///         They are logged once under <see cref="PolicyLoggerCategoryName"/>
///         at Information/Warning with their real status code, the typed code
///         and the module id, and the span carries those as tags instead of an
///         error status. Recording them as "Unhandled exception … status=500"
///         inflated exception telemetry and tripped 5xx alerts for traffic that
///         was behaving exactly as designed (Codex P2-1 on Spec 097).</item>
///   <item>Mapping <see cref="ModuleProvisioningException"/> (Spec 097 §9: a
///         module could not be switched on because a provisioning contributor
///         threw, and the toggle was therefore not persisted) to a
///         <b>500</b> with a typed body — <c>code: "module.provisioning_failed"</c>,
///         <c>moduleId</c>, <c>contributor</c> — so the Admin UI can tell the
///         operator exactly what did not apply and offer a retry. It is
///         deliberately <em>not</em> a policy response: a contributor throwing
///         is a genuine fault, so it keeps the unhandled-exception log entry
///         (with the inner chain) and the error-stamped span. The inner
///         exception's message only reaches the client in Development.</item>
///   <item>Returning structured error JSON in dev / non-prod with the
///         exception type, message, and inner chain; production responses
///         stay opaque (<c>"An internal error occurred."</c>).</item>
/// </list>
///
/// CORS headers on error responses are NOT this middleware's
/// responsibility — they come from <c>UseCors</c> which registers an
/// <c>OnStarting</c> callback earlier in the request, ensuring any
/// response (success or error) carries the right
/// <c>Access-Control-Allow-*</c> headers.
/// </remarks>
public static class ExceptionHandlerConfiguration
{
    /// <summary>
    /// Logger category name used for unhandled exceptions. Stable so KQL
    /// filters can pin to it: <c>traces | where customDimensions.CategoryName == "Aonik.UnhandledException"</c>.
    /// </summary>
    private const string LoggerCategoryName = "Aonik.UnhandledException";

    /// <summary>
    /// Logger category for expected policy responses (module disabled, module dependency, permission
    /// denied). Distinct from <see cref="LoggerCategoryName"/> on purpose: a KQL filter pinned to the
    /// unhandled category must never see them, and a dashboard that wants them can pin to this one:
    /// <c>traces | where customDimensions.CategoryName == "Aonik.PolicyResponse"</c>.
    /// </summary>
    private const string PolicyLoggerCategoryName = "Aonik.PolicyResponse";

    /// <summary>The typed code logged for <see cref="PermissionDeniedException"/>, which carries none of its own.</summary>
    private const string PermissionDeniedCode = "permission.denied";

    /// <summary>
    /// Adds the Aonik global exception handler to the request pipeline.
    /// Should sit early in the pipeline — after <c>UseRouting</c> /
    /// <c>UseCors</c> but before the auth and endpoint middleware — so it
    /// catches anything that bubbles up from a handler.
    /// </summary>
    public static IApplicationBuilder UseAonikExceptionHandler(
        this IApplicationBuilder app,
        IHostEnvironment environment)
    {
        // Error-detail disclosure is gated strictly on IHostEnvironment.IsDevelopment() —
        // i.e. ASPNETCORE_ENVIRONMENT == "Development". A literal "dev" environment string
        // (used for cloud-slot naming) is NOT a development environment and must NEVER
        // leak stack traces to clients.
        var includeDetails = environment.IsDevelopment();

        return app.Use(async (context, next) =>
        {
            try
            {
                await next();
            }
            catch (Exception ex)
            {
                // Expected policy responses are classified BEFORE the unconditional error logging and
                // span stamping: they are 403/409 answers the pipeline produces on purpose, not faults.
                // The response bodies below are unchanged either way.
                var policy = ClassifyPolicyResponse(ex);
                if (policy is { } expected)
                {
                    LogPolicyResponse(context, ex, expected);
                    StampActivity(expected);
                }
                else
                {
                    LogException(context, ex);
                    StampActivity(ex);
                }

                if (context.Response.HasStarted)
                {
                    // Response already on the wire — can't change it. Re-throw
                    // so the host's default fallback runs.
                    throw;
                }

                await WriteErrorResponseAsync(context, ex, includeDetails);
            }
        });
    }

    private static void LogException(HttpContext context, Exception ex)
    {
        var logger = context.RequestServices
            .GetService<ILoggerFactory>()
            ?.CreateLogger(LoggerCategoryName);

        var innermost = GetInnermost(ex);
        logger?.LogError(
            ex,
            "Unhandled exception on {Method} {Path} (status=500, exceptionType={ExceptionType}, exceptionMessage={ExceptionMessage}, innerType={InnerType}, innerMessage={InnerMessage}, chain={ExceptionChain})",
            context.Request.Method,
            context.Request.Path,
            ex.GetType().FullName,
            ex.Message,
            innermost.GetType().FullName,
            innermost.Message,
            FlattenChain(ex));
    }

    private static void StampActivity(Exception ex)
    {
        var activity = Activity.Current;
        if (activity is null) return;

        activity.SetTag("error", true);
        activity.SetTag("error.type", ex.GetType().FullName);
        activity.SetTag("error.message", ex.Message);
        activity.SetTag("aonik.unhandled_exception", true);
        activity.SetStatus(ActivityStatusCode.Error, ex.Message);
    }

    /// <summary>
    /// An exception the pipeline throws on purpose to produce a specific non-5xx answer. Carries what
    /// the log line and the span need; the response body itself is still written by
    /// <see cref="WriteErrorResponseAsync"/> so the client-facing shape stays in one place.
    /// </summary>
    private readonly record struct PolicyResponse(
        int StatusCode,
        string Code,
        string? ModuleId,
        string? Detail,
        LogLevel Level);

    /// <summary>
    /// Returns the policy classification for <paramref name="ex"/>, or null when it is a genuine
    /// unhandled exception. Keep this list to exceptions that mean "the caller may not do this here":
    /// validation, not-found and concurrency outcomes keep their existing logging deliberately.
    /// </summary>
    private static PolicyResponse? ClassifyPolicyResponse(Exception ex) => ex switch
    {
        // A module the tenant switched off (Spec 097 §11): the intended, routine answer for every
        // request into that module — the noisiest of the three by far, hence Information.
        ModuleDisabledException disabled => new PolicyResponse(
            StatusCodes.Status403Forbidden, disabled.Code, disabled.ModuleId, Detail: null, LogLevel.Information),

        // A toggle that would leave the dependency graph inconsistent (Spec 097 §9): a host admin's
        // request the UI is designed to resubmit with the cascade made explicit.
        ModuleDependencyException dependency => new PolicyResponse(
            StatusCodes.Status409Conflict, dependency.Code, dependency.ModuleId,
            Detail: string.Join(",", dependency.RelatedModuleIds), LogLevel.Information),

        // A concurrent change to the same tenant's module set (Spec 097 §9): nothing was written and
        // the caller re-submits, so it is an expected answer rather than a fault — but Warning, since
        // a steady stream of them means two operators are fighting over one tenant.
        ModuleConcurrencyException concurrent => new PolicyResponse(
            StatusCodes.Status409Conflict, concurrent.Code, ModuleId: null,
            Detail: concurrent.TenantId.ToString(), LogLevel.Warning),

        // A permission the caller lacks: expected, but worth a Warning — repeated denials for one
        // principal are a signal an operator wants to see.
        PermissionDeniedException denied => new PolicyResponse(
            StatusCodes.Status403Forbidden, PermissionDeniedCode, ModuleId: null, denied.PermissionKey, LogLevel.Warning),

        _ => null,
    };

    private static void LogPolicyResponse(HttpContext context, Exception ex, in PolicyResponse policy)
    {
        var logger = context.RequestServices
            .GetService<ILoggerFactory>()
            ?.CreateLogger(PolicyLoggerCategoryName);

        // No exception object on purpose: there is no stack worth keeping for a policy answer, and
        // attaching one is exactly what made these look like faults in the exception explorer.
        logger?.Log(
            policy.Level,
            "Policy response on {Method} {Path} (status={StatusCode}, code={PolicyCode}, moduleId={ModuleId}, detail={Detail}, exceptionType={ExceptionType}): {PolicyMessage}",
            context.Request.Method,
            context.Request.Path,
            policy.StatusCode,
            policy.Code,
            policy.ModuleId,
            policy.Detail,
            ex.GetType().FullName,
            ex.Message);
    }

    private static void StampActivity(in PolicyResponse policy)
    {
        var activity = Activity.Current;
        if (activity is null) return;

        // Tags, not an error status: the span completed the way the policy intends. The HTTP status
        // itself is stamped by the ASP.NET Core instrumentation from the response we are about to write.
        activity.SetTag("aonik.policy.code", policy.Code);
        activity.SetTag("aonik.policy.status_code", policy.StatusCode);
        if (policy.ModuleId is not null)
        {
            activity.SetTag("aonik.module_id", policy.ModuleId);
        }
    }

    private static async Task WriteErrorResponseAsync(
        HttpContext context,
        Exception ex,
        bool includeDetails)
    {
        // Order: most-specific first. Each branch returns its own status + payload shape so the
        // front-end can switch on `error` field rather than parsing message strings.
        switch (ex)
        {
            case PermissionDeniedException permissionDenied:
                await WriteJsonAsync(context, StatusCodes.Status403Forbidden, new
                {
                    error = ex.Message,
                    permissionKey = permissionDenied.PermissionKey,
                });
                return;

            case ModuleDisabledException moduleDisabled:
                // 403 with a typed code — the module exists but is switched off for this tenant
                // (Spec 097 §11). The Admin UI routes this to its module-disabled page; a 404 would
                // hide "disabled" behind "does not exist" for anyone debugging a customer report.
                await WriteJsonAsync(context, StatusCodes.Status403Forbidden, new
                {
                    error = ex.Message,
                    code = moduleDisabled.Code,
                    moduleId = moduleDisabled.ModuleId,
                });
                return;

            case ModuleConcurrencyException moduleConcurrency:
                // 409 Conflict — another request changed this tenant's module set between the
                // dependency checks and the commit (Spec 097 §9). Nothing was written; the client
                // re-reads and re-submits.
                await WriteJsonAsync(context, StatusCodes.Status409Conflict, new
                {
                    error = ex.Message,
                    code = moduleConcurrency.Code,
                    tenantId = moduleConcurrency.TenantId,
                });
                return;

            case ModuleDependencyException moduleDependency:
                // 409 Conflict — a module toggle that would leave the tenant's set dependency-
                // inconsistent (Spec 097 §9). relatedModuleIds lists the missing dependencies or the
                // still-enabled dependents so the UI can offer the cascade explicitly.
                await WriteJsonAsync(context, StatusCodes.Status409Conflict, new
                {
                    error = ex.Message,
                    code = moduleDependency.Code,
                    moduleId = moduleDependency.ModuleId,
                    relatedModuleIds = moduleDependency.RelatedModuleIds,
                });
                return;

            case ModuleProvisioningException provisioning:
                // 500 with a typed code — the toggle was NOT applied because the module's provisioning
                // contributor threw (Spec 097 §9). A 5xx is the honest status (the server, not the
                // caller, failed) and it is retryable because contributors are idempotent; the typed
                // body is what lets the UI say so instead of "something went wrong". The inner
                // exception's message (SQL text, partner responses) stays server-side outside Development.
                await WriteJsonAsync(context, StatusCodes.Status500InternalServerError, new
                {
                    error = includeDetails
                        ? ex.Message
                        : $"Module '{provisioning.ModuleId}' could not be enabled: provisioning by {provisioning.Contributor} failed. No module settings were changed; retry once the underlying fault is resolved.",
                    code = provisioning.Code,
                    moduleId = provisioning.ModuleId,
                    contributor = provisioning.Contributor,
                });
                return;

            case SpeechLibraryUsageBlockedException usageBlocked:
                // 409 Conflict — UI surfaces the blocking recipes inline so the user can fix the
                // dependency directly. See spec 024 §"Service Surface" Validation rules.
                await WriteJsonAsync(context, StatusCodes.Status409Conflict, new
                {
                    error = ex.Message,
                    code = "speech_library.usage_blocked",
                    usage = usageBlocked.Usage,
                });
                return;

            case SpeechLibraryImmutableBuiltInException immutable:
                await WriteJsonAsync(context, StatusCodes.Status409Conflict, new
                {
                    error = ex.Message,
                    code = "speech_library.immutable_built_in",
                    builtInId = immutable.BuiltInId,
                });
                return;

            case SpeechLibraryValidationException validation:
                await WriteJsonAsync(context, StatusCodes.Status422UnprocessableEntity, new
                {
                    error = ex.Message,
                    code = "speech_library.validation",
                    fieldName = validation.FieldName,
                });
                return;

            case OptionValidationException optionValidation:
                // Spec 066 §9 — invalid customer or admin option input is a client fault, not a
                // server error. The rule id travels with it so storefronts can react precisely
                // (e.g. V2 "this product does not offer that choice" vs V3 "it was withdrawn").
                await WriteJsonAsync(context, StatusCodes.Status400BadRequest, new
                {
                    error = ex.Message,
                    code = "commerce.option_validation",
                    rule = optionValidation.RuleId,
                });
                return;

            case StorefrontValidationException:
                // Spec 070 §6 — unknown facet keys/values, label-for-value submissions, invalid
                // sort combinations: a storefront bug should be loud, and a client fault, not 500.
                await WriteJsonAsync(context, StatusCodes.Status400BadRequest, new
                {
                    error = ex.Message,
                    code = "commerce.storefront_validation",
                });
                return;

            case NotFoundException:
                await WriteJsonAsync(context, StatusCodes.Status404NotFound, new { error = ex.Message });
                return;

            case InvalidStateException:
                await WriteJsonAsync(context, StatusCodes.Status422UnprocessableEntity, new { error = ex.Message });
                return;

            case BoxCheckoutDriftException boxDrift:
                // Spec 068 A18 — the box changed since the client last saw it; nothing was
                // reserved or created. The refreshed box + changes travel in the body so the
                // customer reviews the changed meal or price, then resubmits.
                await WriteJsonAsync(context, StatusCodes.Status409Conflict, new
                {
                    error = "commerce.box_drift",
                    message = boxDrift.Message,
                    box = boxDrift.Refreshed.Box,
                    quote = boxDrift.Refreshed.Quote,
                    changes = boxDrift.Refreshed.Changes,
                });
                return;

            case DbUpdateConcurrencyException:
                // Writers that guard shared invariants deliberately contend on a row-version token
                // (e.g. Spec 066's option-group and product touches). The loser of that race is a
                // well-defined outcome, not a server fault: tell the client to re-read and retry.
                await WriteJsonAsync(context, StatusCodes.Status409Conflict, new
                {
                    error = "The resource was modified by another operation. Re-read the current state and retry.",
                    code = "concurrency_conflict",
                });
                return;
        }

        // Default: unexpected — 500 with detail in dev, opaque in prod.
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";

        const string topLevelMessage = "An internal error occurred.";
        object payload;
        if (includeDetails)
        {
            var innermost = GetInnermost(ex);
            payload = new
            {
                error = topLevelMessage,
                exceptionType = ex.GetType().FullName,
                exceptionMessage = ex.Message,
                innerType = innermost.GetType().FullName,
                innerMessage = innermost.Message,
                exceptionChain = FlattenChain(ex),
                path = context.Request.Path.Value,
            };
        }
        else
        {
            payload = new { error = topLevelMessage };
        }

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }

    private static async Task WriteJsonAsync(HttpContext context, int statusCode, object payload)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }

    private static Exception GetInnermost(Exception ex)
    {
        var current = ex;
        while (current.InnerException is not null) current = current.InnerException;
        return current;
    }

    private static string FlattenChain(Exception root)
    {
        var sb = new StringBuilder();
        for (var current = (Exception?)root; current is not null; current = current.InnerException)
        {
            if (sb.Length > 0) sb.Append(" -> ");
            sb.Append(current.GetType().FullName).Append(": ").Append(current.Message);
        }
        return sb.ToString();
    }
}
