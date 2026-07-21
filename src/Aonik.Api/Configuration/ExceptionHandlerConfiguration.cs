using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Aonik.Commerce.Services.Catalog;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ai.Speech;

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
                LogException(context, ex);
                StampActivity(ex);

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

            case NotFoundException:
                await WriteJsonAsync(context, StatusCodes.Status404NotFound, new { error = ex.Message });
                return;

            case InvalidStateException:
                await WriteJsonAsync(context, StatusCodes.Status422UnprocessableEntity, new { error = ex.Message });
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
