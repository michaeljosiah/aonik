using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;

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
