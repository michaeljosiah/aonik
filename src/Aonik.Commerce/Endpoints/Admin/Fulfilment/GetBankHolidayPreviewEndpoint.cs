using FastEndpoints;
using Microsoft.AspNetCore.Http;

using Aonik.Commerce.Contracts.Models.Fulfilment;
using Aonik.Commerce.Services.Fulfilment;

namespace Aonik.Commerce.Endpoints.Admin.Fulfilment;

public sealed class GetBankHolidayPreviewEndpoint(IBankHolidaySource source)
    : EndpointWithoutRequest<BankHolidayPreviewDto>
{
    public override void Configure()
    {
        Get("/commerce/admin/fulfilment-calendar/bank-holidays/{region}");
        Policies("AdminReadPolicy");
        Summary(summary =>
        {
            summary.Summary = "Preview official UK bank holidays for a region.";
            summary.Description = "Review the dates, then save selected closures through the fulfilment calendar. This read does not change the calendar.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        var region = Route<string>("region");
        if (!BankHolidayRegions.IsSupported(region))
        {
            await Send.ResultAsync(Results.BadRequest(new
            {
                code = "commerce.invalid_bank_holiday_region",
                message = "Choose england-and-wales, scotland or northern-ireland."
            }));
            return;
        }

        var preview = await source.GetAsync(region!, ct);
        if (preview is null)
        {
            await Send.ResultAsync(Results.Json(new
            {
                code = "commerce.bank_holidays_unavailable",
                message = "The bank-holiday source is temporarily unavailable. Please retry."
            }, statusCode: StatusCodes.Status503ServiceUnavailable));
            return;
        }

        await Send.OkAsync(preview, ct);
    }
}
