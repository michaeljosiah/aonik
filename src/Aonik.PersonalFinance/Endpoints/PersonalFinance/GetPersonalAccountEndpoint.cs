using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.PersonalFinance;

internal sealed class GetPersonalAccountEndpoint : EndpointWithoutRequest<PersonalAccountResponse>
{
    private readonly IPersonalAccountService _personalAccountService;

    public GetPersonalAccountEndpoint(IPersonalAccountService personalAccountService)
    {
        _personalAccountService = personalAccountService;
    }

    public override void Configure()
    {
        Get("/personal-finance/accounts/{id}");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Get a personal account by ID";
            s.Description = "Returns the details of a single personal finance account including its name, type, currency, and current balance.";
            s.Response(200, "Account returned successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Account not found");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var response = await _personalAccountService.GetAccountAsync(id, ct);

        if (response == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(response, ct);
    }
}
