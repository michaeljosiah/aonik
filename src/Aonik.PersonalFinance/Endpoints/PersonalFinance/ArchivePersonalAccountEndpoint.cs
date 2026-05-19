using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.PersonalFinance;

internal sealed class ArchivePersonalAccountEndpoint : EndpointWithoutRequest
{
    private readonly IPersonalAccountService _personalAccountService;

    public ArchivePersonalAccountEndpoint(IPersonalAccountService personalAccountService)
    {
        _personalAccountService = personalAccountService;
    }

    public override void Configure()
    {
        Post("/personal-finance/accounts/{id}/archive");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Archive a personal account";
            s.Description = "Archives a personal finance account, hiding it from active views while preserving its transaction history.";
            s.Response(204, "Account archived successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Account not found");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");

        try
        {
            await _personalAccountService.ArchiveAccountAsync(id, ct);
            await Send.NoContentAsync(ct);
        }
        catch (InvalidOperationException)
        {
            await Send.NotFoundAsync(ct);
        }
    }
}
