using Aonik.Commerce.Contracts.Api.Catalog;
using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Catalog;

public class CreateCategoryEndpoint : Endpoint<CreateCategoryRequest, ProductCategoryDto>
{
    private readonly IProductService _products;

    public CreateCategoryEndpoint(IProductService products) => _products = products;

    public override void Configure()
    {
        Post("/commerce/admin/categories");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary = "Create a catalog category.");
    }

    public override async Task HandleAsync(CreateCategoryRequest req, CancellationToken ct)
    {
        var result = await _products.CreateCategoryAsync(
            new CreateCategoryCommand(req.Slug, req.Name, req.ParentCategoryId, req.SortOrder), ct);
        await Send.OkAsync(result, ct);
    }
}
