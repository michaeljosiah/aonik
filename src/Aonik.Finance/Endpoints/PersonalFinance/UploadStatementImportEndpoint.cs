using Microsoft.AspNetCore.Http;

using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.PersonalFinance;

internal sealed class UploadStatementImportEndpoint : EndpointWithoutRequest<StatementImportResponse>
{
    private readonly IStatementImportService _statementImportService;

    public UploadStatementImportEndpoint(IStatementImportService statementImportService)
    {
        _statementImportService = statementImportService;
    }

    public override void Configure()
    {
        Post("/personal-finance/imports/statements");
        Policies("UserPolicy");
        AllowFileUploads();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (Files.Count == 0)
        {
            await SendValidationErrorAsync("Statement file is required.", ct);
            return;
        }

        var file = Files[0];
        if (file.Length == 0)
        {
            await SendValidationErrorAsync("Statement file is empty.", ct);
            return;
        }

        var form = await HttpContext.Request.ReadFormAsync(ct);
        if (!Guid.TryParse(form["personalAccountId"], out var personalAccountId) || personalAccountId == Guid.Empty)
        {
            await SendValidationErrorAsync("personalAccountId is required.", ct);
            return;
        }

        var request = new UploadStatementImportRequest(
            personalAccountId,
            file.FileName,
            string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);

        try
        {
            await using var stream = file.OpenReadStream();
            var response = await _statementImportService.UploadStatementAsync(request, stream, ct);
            await Send.OkAsync(response, ct);
        }
        catch (ArgumentException ex)
        {
            await SendValidationErrorAsync(ex.Message, ct);
        }
        catch (InvalidOperationException ex)
        {
            ThrowError(ex.Message, 409);
        }
    }

    private async Task SendValidationErrorAsync(string message, CancellationToken ct)
    {
        HttpContext.Response.StatusCode = 422;
        await HttpContext.Response.WriteAsJsonAsync(new { error = message }, ct);
    }
}
