using Aonik.Commerce.Contracts.Api.Catalog;
using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Catalog;

public class UpdateOptionChoiceEndpoint : Endpoint<UpdateOptionChoiceRequest, OptionChoiceDto>
{
    private readonly IProductOptionService _options;

    public UpdateOptionChoiceEndpoint(IProductOptionService options) => _options = options;

    public override void Configure()
    {
        Put("/commerce/admin/option-choices/{choiceId:guid}");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary = "Update a choice. The key and the default flag are not editable here.");
    }

    public override async Task HandleAsync(UpdateOptionChoiceRequest req, CancellationToken ct)
    {
        var result = await _options.UpdateChoiceAsync(
            Route<Guid>("choiceId"),
            new UpdateOptionChoiceCommand(req.Label, req.Note, req.Price, req.SortOrder, req.IsActive),
            ct);

        await Send.OkAsync(result, ct);
    }
}
