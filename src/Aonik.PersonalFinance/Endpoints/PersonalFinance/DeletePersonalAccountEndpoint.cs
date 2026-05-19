using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.PersonalFinance;

internal sealed class DeletePersonalAccountEndpoint : EndpointWithoutRequest
{
    private readonly IPersonalAccountService _personalAccountService;

    public DeletePersonalAccountEndpoint(IPersonalAccountService personalAccountService)
    {
        _personalAccountService = personalAccountService;
    }

    public override void Configure()
    {
        Delete("/personal-finance/accounts/{id}");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Delete a manual personal account";
            s.Description = "Permanently deletes a manually created personal account and all its transactions. Linked accounts cannot be deleted — disconnect them instead.";
            s.Response(204, "Account deleted successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Account not found");
            s.Response(422, "Account cannot be deleted (e.g. linked account)");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");

        try
        {
            await _personalAccountService.DeleteManualAccountAsync(id, ct);
            await Send.NoContentAsync(ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message, 422);
        }
        catch (InvalidOperationException)
        {
            await Send.NotFoundAsync(ct);
        }
    }
}
