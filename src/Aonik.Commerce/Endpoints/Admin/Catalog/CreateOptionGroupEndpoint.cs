using Aonik.Commerce.Contracts.Api.Catalog;
using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Catalog;

/// <summary>Authoring endpoints for the Spec 066 option catalogue.</summary>
public class CreateOptionGroupEndpoint : Endpoint<CreateOptionGroupRequest, OptionGroupDto>
{
    private readonly IProductOptionService _options;

    public CreateOptionGroupEndpoint(IProductOptionService options) => _options = options;

    public override void Configure()
    {
        Post("/commerce/admin/option-groups");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary = "Create an option group.");
    }

    public override async Task HandleAsync(CreateOptionGroupRequest req, CancellationToken ct)
    {
        var result = await _options.CreateGroupAsync(
            new CreateOptionGroupCommand(
                req.Key,
                req.Label,
                req.HelpText,
                req.SelectionMode ?? Entities.Catalog.OptionSelectionModes.One,
                req.Currency ?? "GBP",
                req.SortOrder),
            ct);

        await Send.OkAsync(result, ct);
    }
}
