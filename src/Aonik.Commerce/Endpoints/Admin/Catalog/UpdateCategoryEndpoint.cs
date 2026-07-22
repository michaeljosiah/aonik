using Aonik.Commerce.Contracts.Api.Catalog;
using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Catalog;

/// <summary>Update name/parent/sort/IsActive — categories retire, never delete (Spec 070 §11).</summary>
public class UpdateCategoryEndpoint : Endpoint<UpdateCategoryRequest, ProductCategoryDto>
{
    private readonly IProductService _products;

    public UpdateCategoryEndpoint(IProductService products) => _products = products;

    public override void Configure()
    {
        Put("/commerce/admin/categories/{categoryId:guid}");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary = "Update a category. Omitted members are unchanged; clearParent moves it to the root.");
    }

    public override async Task HandleAsync(UpdateCategoryRequest req, CancellationToken ct)
    {
        var result = await _products.UpdateCategoryAsync(
            Route<Guid>("categoryId"),
            new UpdateCategoryCommand(req.Name, req.ParentCategoryId, req.ClearParent, req.SortOrder, req.IsActive),
            ct);
        await Send.OkAsync(result, ct);
    }
}
