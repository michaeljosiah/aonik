using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.PersonalFinance;

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
    }

    public override async Task HandleAsync(ListPersonalAccountsRequest req, CancellationToken ct)
    {
        var response = await _personalAccountService.ListAccountsAsync(req.IncludeArchived, ct);
        await Send.OkAsync(response, ct);
    }
}
