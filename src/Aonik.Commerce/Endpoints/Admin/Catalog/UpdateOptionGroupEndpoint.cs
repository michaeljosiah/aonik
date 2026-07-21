using Aonik.Commerce.Contracts.Api.Catalog;
using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Catalog;

public class UpdateOptionGroupEndpoint : Endpoint<UpdateOptionGroupRequest, OptionGroupDto>
{
    private readonly IProductOptionService _options;

    public UpdateOptionGroupEndpoint(IProductOptionService options) => _options = options;

    public override void Configure()
    {
        Put("/commerce/admin/option-groups/{groupId:guid}");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary = "Update an option group. The key is immutable.");
    }

    public override async Task HandleAsync(UpdateOptionGroupRequest req, CancellationToken ct)
    {
        var result = await _options.UpdateGroupAsync(
            Route<Guid>("groupId"),
            new UpdateOptionGroupCommand(
                req.Label,
                req.HelpText,
                req.SelectionMode ?? Entities.Catalog.OptionSelectionModes.One,
                req.Currency ?? "GBP",
                req.SortOrder,
                req.IsActive),
            ct);

        await Send.OkAsync(result, ct);
    }
}
