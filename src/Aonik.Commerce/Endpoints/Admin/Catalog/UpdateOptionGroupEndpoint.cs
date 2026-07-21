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
            // Every optional member passes through as null when omitted, which the service reads as
            // "leave it alone". Applying creation defaults here would let a label-only edit silently
            // re-shape the group, redenominate every stored choice price as GBP, deactivate the
            // group, or reset its ordering.
            new UpdateOptionGroupCommand(
                req.Label,
                req.HelpText,
                req.SelectionMode,
                req.Currency,
                req.SortOrder,
                req.IsActive),
            ct);

        await Send.OkAsync(result, ct);
    }
}
