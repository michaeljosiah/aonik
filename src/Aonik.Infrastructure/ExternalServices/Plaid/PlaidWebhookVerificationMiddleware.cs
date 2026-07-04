using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Aonik.Infrastructure.ExternalServices.Plaid;

/// <summary>Pipeline wiring for <see cref="PlaidWebhookVerificationMiddleware"/>.</summary>
public static class PlaidWebhookVerificationMiddlewareExtensions
{
    /// <summary>
    /// Verifies the Plaid webhook JWS signature ahead of the anonymous webhook endpoints.
    /// Register before <c>UseFastEndpoints</c>.
    /// </summary>
    public static IApplicationBuilder UsePlaidWebhookVerification(this IApplicationBuilder app)
        => app.UseMiddleware<PlaidWebhookVerificationMiddleware>();
}

/// <summary>
/// Verifies the Plaid webhook signature before the anonymous webhook endpoints run (issue
/// H13). The endpoints must stay anonymous — Plaid does not carry our auth — so the signed
/// <c>Plaid-Verification</c> JWS is the security boundary, and it is enforced here, ahead of
/// model binding and the handler. A request that fails verification is rejected with 401 and
/// never reaches the processor.
/// </summary>
internal sealed class PlaidWebhookVerificationMiddleware
{
    // The two anonymous Plaid webhook endpoints (Finance + PersonalFinance account links).
    private static readonly PathString FinanceWebhookPath = new("/admin/accounts/webhooks/plaid");
    private static readonly PathString PersonalFinanceWebhookPath = new("/personal-finance/account-links/webhooks/plaid");
    private const string VerificationHeader = "Plaid-Verification";

    // Plaid webhooks are a few KB; this generous cap bounds the in-memory buffering of an
    // anonymous, pre-auth request so a large body can't be used as a memory-pressure lever.
    private const int MaxBodyBytes = 256 * 1024;

    private readonly RequestDelegate _next;

    public PlaidWebhookVerificationMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(
        HttpContext context,
        IPlaidWebhookVerifier verifier,
        ILogger<PlaidWebhookVerificationMiddleware> logger)
    {
        if (!IsPlaidWebhook(context.Request))
        {
            await _next(context);
            return;
        }

        // In simulation/dev (real Plaid API not configured) there is no signature to check —
        // the webhook is not a genuine Plaid callback — so verification is inert by design.
        if (!verifier.IsEnabled)
        {
            await _next(context);
            return;
        }

        // Reject an oversized body up front when the length is known.
        if (context.Request.ContentLength is > MaxBodyBytes)
        {
            logger.LogWarning("Rejected Plaid webhook: declared body length exceeds {Max} bytes.", MaxBodyBytes);
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
        }

        // Read the raw body (capped, so a chunked request can't stream past the limit either) so
        // its hash can be compared to the signed request_body_sha256, then rewind so FastEndpoints
        // can still bind it. Buffering makes the re-read safe.
        context.Request.EnableBuffering();
        var body = await ReadBoundedBodyAsync(context.Request.Body, MaxBodyBytes, context.RequestAborted);
        context.Request.Body.Position = 0;
        if (body is null)
        {
            logger.LogWarning("Rejected Plaid webhook: body exceeds {Max} bytes.", MaxBodyBytes);
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
        }

        var header = context.Request.Headers[VerificationHeader].FirstOrDefault();
        if (!await verifier.VerifyAsync(header, body, context.RequestAborted))
        {
            logger.LogWarning(
                "Rejected unverified Plaid webhook on {Path}.", context.Request.Path.Value);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await _next(context);
    }

    private static bool IsPlaidWebhook(HttpRequest request)
        => HttpMethods.IsPost(request.Method)
           && (request.Path.Equals(FinanceWebhookPath, StringComparison.OrdinalIgnoreCase)
               || request.Path.Equals(PersonalFinanceWebhookPath, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Reads up to <paramref name="maxBytes"/> from <paramref name="body"/>; returns null if the
    /// stream has more than that (too large), so a chunked request with no declared length still
    /// cannot force an unbounded buffer.
    /// </summary>
    private static async Task<byte[]?> ReadBoundedBodyAsync(Stream body, int maxBytes, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = await body.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > maxBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
