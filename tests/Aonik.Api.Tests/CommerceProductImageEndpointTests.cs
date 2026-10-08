using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Infrastructure.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Storage;

using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Aonik.Api.Tests;

public class CommerceProductImageEndpointTests(ProductImageTestFactory factory) : IClassFixture<ProductImageTestFactory>
{
    [Fact]
    public async Task Upload_Should_ReturnADraft_AndPreserveAltInTheSavedHero()
    {
        var tenantId = Guid.NewGuid();
        var admin = await factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithTenant(tenantId).WithRoles("Operations"));
        var productId = await SeedProductAsync(tenantId);
        using var form = await ImageFormAsync();

        var response = await admin.PostAsync($"/commerce/admin/products/{productId}/images", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var draft = (await response.Content.ReadFromJsonAsync<ProductImageUploadDto>())!;
        draft.Url.Should().StartWith("/storage/products/").And.EndWith(".jpg");
        draft.AltText.Should().Be("Rice in a bowl");
        factory.Uploads.Should().Contain(x => x.TenantId == tenantId && x.ProductId == productId);
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenantId;
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        (await db.ProductMedia.CountAsync(x => x.ProductId == productId)).Should().Be(0);

        var saved = await admin.PutAsJsonAsync($"/commerce/admin/products/{productId}/media", new
        {
            items = new[]
            {
                new { url = "https://example.test/menu.pdf", kind = "doc", altText = "Menu" },
                new { url = draft.Url, kind = "image", altText = draft.AltText }
            }
        });
        saved.StatusCode.Should().Be(HttpStatusCode.OK);
        var reordered = await saved.Content.ReadFromJsonAsync<List<ProductMediaDto>>();
        reordered.Should().HaveCount(2);
        reordered![1].AltText.Should().Be(draft.AltText);
        var cards = await admin.GetFromJsonAsync<System.Text.Json.JsonElement>("/commerce/catalog/products");
        var card = cards.GetProperty("items").EnumerateArray().Single();
        card.GetProperty("heroImageAltText").GetString().Should().Be(draft.AltText);
        card.GetProperty("isPlaceholder").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Upload_Should_RequireAdmin_AndHideAnotherTenantsProduct()
    {
        var tenantId = Guid.NewGuid();
        await factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithTenant(tenantId).WithRoles("Operations"));
        var productId = await SeedProductAsync(tenantId);
        var anonymous = factory.CreateClient();
        anonymous.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        using var anonymousForm = await ImageFormAsync();
        (await anonymous.PostAsync($"/commerce/admin/products/{productId}/images", anonymousForm)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var customer = await factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithTenant(tenantId).WithRoles("PersonalUser"));
        using var customerForm = await ImageFormAsync();
        (await customer.PostAsync($"/commerce/admin/products/{productId}/images", customerForm)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var otherAdmin = await factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithTenant(Guid.NewGuid()).WithRoles("Operations"));
        using var foreignForm = await ImageFormAsync();
        (await otherAdmin.PostAsync($"/commerce/admin/products/{productId}/images", foreignForm)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        factory.Uploads.Should().NotContain(x => x.ProductId == productId);
    }

    [Theory]
    [InlineData("alt")]
    [InlineData("bytes")]
    [InlineData("two-files")]
    public async Task Upload_Should_RejectInvalidMultipartWithoutStorageWrites(string invalid)
    {
        var tenantId = Guid.NewGuid();
        var admin = await factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithTenant(tenantId).WithRoles("Operations"));
        var productId = await SeedProductAsync(tenantId);
        using var form = await ImageFormAsync(invalid == "bytes" ? "<svg />"u8.ToArray() : null, invalid == "alt" ? " " : "A dish");
        if (invalid == "two-files") form.Add(new ByteArrayContent(new byte[] { 1 }), "file", "extra.jpg");

        var response = await admin.PostAsync($"/commerce/admin/products/{productId}/images", form);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        factory.Uploads.Should().NotContain(x => x.ProductId == productId);
    }

    private async Task<Guid> SeedProductAsync(Guid tenantId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenantId;
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        var product = new Product { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Dish", Slug = "dish", Status = ProductStatuses.Active };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product.Id;
    }

    private static async Task<MultipartFormDataContent> ImageFormAsync(byte[]? bytes = null, string alt = " Rice in a bowl ")
    {
        if (bytes is null)
        {
            using var input = new MemoryStream();
            using var image = new Image<Rgba32>(2, 2);
            await image.SaveAsPngAsync(input);
            bytes = input.ToArray();
        }
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        // Only the actual bytes determine what is accepted and what storage suffix is used.
        file.Headers.ContentType = new MediaTypeHeaderValue("text/html");
        form.Add(file, "file", "misleading.html");
        form.Add(new StringContent(alt), "altText");
        return form;
    }
}

public sealed class ProductImageTestFactory : CustomWebApplicationFactory
{
    public ConcurrentQueue<(Guid TenantId, Guid ProductId)> Uploads { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.AddKeyedSingleton<IFileStore>(FileStoreKeys.ProductImages, new TestFileStore(Uploads));
        });
    }

    private sealed class TestFileStore(ConcurrentQueue<(Guid TenantId, Guid ProductId)> uploads) : IFileStore
    {
        public Task<FileUploadResult> UploadAsync(Guid tenantId, Guid ownerEntityId, Stream fileStream,
            string fileName, string contentType, CancellationToken cancellationToken = default)
        {
            fileName.Should().Be("product.jpg");
            contentType.Should().Be("image/jpeg");
            uploads.Enqueue((tenantId, ownerEntityId));
            return Task.FromResult(new FileUploadResult("Local", "products", $"{ownerEntityId:N}.jpg",
                contentType, fileName, fileStream.Length, "hash"));
        }

        public string GetUrl(string storageKey) => $"/storage/products/{storageKey}";
        public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<StagedBlob> StageAsync(Guid tenantId, Stream content, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PromoteResult> PromoteAsync(StagedBlob staged, string contentKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
