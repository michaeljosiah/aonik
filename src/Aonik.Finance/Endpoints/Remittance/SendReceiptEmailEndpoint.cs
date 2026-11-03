using Aonik.Authorization;
using Aonik.Finance.Contracts.Services.Remittance;
using Aonik.SharedKernel.Validation;
using FastEndpoints;
using FluentValidation;

namespace Aonik.Finance.Endpoints.Remittance;

public sealed class SendReceiptEmailRequestValidator : Validator<SendReceiptEmailRequest>
{
    public SendReceiptEmailRequestValidator()
    {
        RuleFor(request => request.RequestId).RequiredId();RuleFor(request => request.ContactId).RequiredId();
        RuleFor(request => request.ExpectedEmail).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(request => request.ConfirmSend).Equal(true).WithMessage("Confirm the customer and receipt email before sending.");
    }
}

public sealed class SendReceiptEmailEndpoint(IRemittanceReceiptService receipts) : Endpoint<SendReceiptEmailRequest, ReceiptEmailResult>
{
    public override void Configure()
    {
        Post("/remittance/{id:guid}/receipt/email");Policies("AdminWritePolicy");
        this.RequiresPermission("Payment.Read");this.RequiresPermission("Customers.Read");this.RequiresPermission("Customers.Write");
    }

    public override async Task HandleAsync(SendReceiptEmailRequest req, CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        await Send.OkAsync(await receipts.SendAsync(Route<Guid>("id"), req, ct), ct);
    }
}
