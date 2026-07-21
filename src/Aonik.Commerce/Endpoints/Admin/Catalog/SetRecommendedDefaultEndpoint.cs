using Aonik.Commerce.Contracts.Api.Catalog;
using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Catalog;

/// <summary>
/// Moves a group's recommended default atomically. The only supported way to change it — direct
/// flag writes are rejected (rule V7) because a demote-then-promote pair would transit through a
/// zero- or two-default state.
/// </summary>
public class SetRecommendedDefaultEndpoint : Endpoint<SetRecommendedDefaultRequest, RecommendedDefaultChangeResult>
{
    private readonly IProductOptionService _options;

    public SetRecommendedDefaultEndpoint(IProductOptionService options) => _options = options;

    public override void Configure()
    {
        Put("/commerce/admin/option-groups/{groupId:guid}/recommended-default");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary =
            "Atomically move a group's recommended default. Returns the products whose standard preparation just changed.");
    }

    public override async Task HandleAsync(SetRecommendedDefaultRequest req, CancellationToken ct)
    {
        var result = await _options.SetRecommendedDefaultAsync(Route<Guid>("groupId"), req.ChoiceKey, ct);
        await Send.OkAsync(result, ct);
    }
}
