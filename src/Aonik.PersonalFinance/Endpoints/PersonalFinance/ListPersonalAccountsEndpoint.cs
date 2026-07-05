using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Contracts.Services;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.PersonalFinance.Endpoints;

internal sealed class ListPersonalAccountsRequest
{
    public bool IncludeArchived { get; set; }
}

internal sealed class ListPersonalAccountsEndpoint : Endpoint<ListPersonalAccountsRequest, IReadOnlyList<PersonalAccountResponse>>
{
    private readonly IPersonalAccountService _personalAccountService;

    public ListPersonalAccountsEndpoint(IPersonalAccountService personalAccountService)
    {
        _personalAccountService = personalAccountService;
    }

    public override void Configure()
    {
        Get("/personal-finance/accounts");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "List personal accounts";
            s.Description = "Returns all personal finance accounts for the authenticated user, with an option to include archived accounts.";
            s.Response(200, "Account list returned successfully");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(ListPersonalAccountsRequest req, CancellationToken ct)
    {
        var response = await _personalAccountService.ListAccountsAsync(req.IncludeArchived, ct);
        await Send.OkAsync(response, ct);
    }
}
