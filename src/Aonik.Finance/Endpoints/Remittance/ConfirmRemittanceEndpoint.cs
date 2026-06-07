using Aonik.Finance.Contracts.Api.Remittance;
using Aonik.Finance.Contracts.Services.Remittance;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Remittance;

/// <summary>
/// <c>POST /payabo/remittance/confirm</c> — confirms a quoted remittance: locks the quote, posts the
/// ledger debit, and dispatches the payout. Money movement: authenticated, never anonymous, and
/// idempotent on the required <c>Idempotency-Key</c> header. Spec 036 §10.2.
/// </summary>
public class ConfirmRemittanceEndpoint : Endpoint<ConfirmRemittanceRequest, RemittanceOrderResponse>
{
    private readonly IRemittanceOrderService _remittanceService;

    public ConfirmRemittanceEndpoint(IRemittanceOrderService remittanceService)
    {
        _remittanceService = remittanceService;
    }

    public override void Configure()
    {
        Post("/payabo/remittance/confirm");
        Policies("AdminUserWritePolicy");
        Summary(s =>
        {
            s.Summary = "Confirm a remittance";
            s.Description = "Locks a remittance quote, posts the customer debit, and instructs the payout connector. Requires an Idempotency-Key header; replaying the same key returns the existing order.";
            s.Response(201, "Remittance confirmed");
            s.Response(400, "Invalid request data or missing Idempotency-Key");
            s.Response(401, "Not authenticated");
            s.Response(403, "Caller does not own the requested customer party");
            s.Response(404, "Quote or destination account not found");
            s.Response(422, "Quote expired, account/quote mismatch, or no route");
        });
        Options(x => x.WithTags("Remittance"));
    }

    public override async Task HandleAsync(ConfirmRemittanceRequest req, CancellationToken ct)
    {
        var idempotencyKey = HttpContext.Request.Headers.TryGetValue("Idempotency-Key", out var values)
            ? values.ToString()
            : null;

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            AddError("An Idempotency-Key header is required.");
            await Send.ErrorsAsync(400, ct);
            return;
        }

        try
        {
            var result = await _remittanceService.ConfirmAsync(RemittanceMapping.ToModel(req), idempotencyKey, ct);

            await Send.CreatedAtAsync<GetRemittanceOrderEndpoint>(
                routeValues: new { id = result.OrderId },
                responseBody: RemittanceMapping.ToApi(result),
                cancellation: ct);
        }
        catch (UnauthorizedAccessException ex)
        {
            AddError(ex.Message);
            await Send.ErrorsAsync(403, ct);
        }
        catch (ArgumentException ex)
        {
            AddError(ex.Message);
            await Send.ErrorsAsync(400, ct);
        }
        catch (InvalidOperationException ex)
        {
            AddError(ex.Message);
            await Send.ErrorsAsync(ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase) ? 404 : 422, ct);
        }
    }
}
