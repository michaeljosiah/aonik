using Aonik.Commerce.Contracts.Api.Catalog;
using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Catalog;

public class AddOptionChoiceEndpoint : Endpoint<AddOptionChoiceRequest, OptionChoiceDto>
{
    private readonly IProductOptionService _options;

    public AddOptionChoiceEndpoint(IProductOptionService options) => _options = options;

    public override void Configure()
    {
        Post("/commerce/admin/option-groups/{groupId:guid}/choices");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary = "Add a choice to an option group.");
    }

    public override async Task HandleAsync(AddOptionChoiceRequest req, CancellationToken ct)
    {
        var result = await _options.AddChoiceAsync(
            Route<Guid>("groupId"),
            new AddOptionChoiceCommand(req.Key, req.Label, req.Note, req.Price, req.IsRecommendedDefault, req.SortOrder, req.IsActive),
            ct);

        await Send.OkAsync(result, ct);
    }
}
