using Aonik.Authorization;
using Aonik.Finance.Contracts.Services.Remittance;
using FastEndpoints;
using FluentValidation;

namespace Aonik.Finance.Endpoints.Remittance;

public sealed class ReceiptEmailHistoryRequest
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed class ReceiptEmailHistoryRequestValidator : Validator<ReceiptEmailHistoryRequest>
{
    public ReceiptEmailHistoryRequestValidator()
    {
        RuleFor(request => request.PageNumber).GreaterThan(0);
        RuleFor(request => request.PageSize).InclusiveBetween(1, 50);
        RuleFor(request => request).Must(request => (long)(request.PageNumber - 1) * request.PageSize <= int.MaxValue)
            .WithMessage("The requested history page is too large.");
    }
}

public sealed class GetReceiptEmailHistoryEndpoint(IRemittanceReceiptService receipts) : Endpoint<ReceiptEmailHistoryRequest, ReceiptEmailHistory>
{
    public override void Configure()
    {
        Get("/remittance/{id:guid}/receipt/email/history");
        Policies("AdminReadPolicy");
        this.RequiresPermission("Payment.Read");
        this.RequiresPermission("Customers.Read");
    }

    public override async Task HandleAsync(ReceiptEmailHistoryRequest req, CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        await Send.OkAsync(await receipts.GetEmailHistoryAsync(Route<Guid>("id"), req.PageNumber, req.PageSize, ct), ct);
    }
}
