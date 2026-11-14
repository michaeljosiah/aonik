using Aonik.Application.Options;
using Aonik.Infrastructure.Storage;

using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using Moq;

namespace Aonik.Infrastructure.Tests.Storage;

public class PrivateDocumentFileStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aonik-financial-storage-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task FinancialSource_Should_RoundTripInTheExistingPrivateStore_AndNeverHaveAPublicUrl()
    {
        var settings = new BlobStorageOptions { LocalBasePath = Path.Combine(_root, "App_Data") };
        var store = Store(settings); using var source = new MemoryStream("private invoice"u8.ToArray());

        var stored = await store.UploadPrivateDocumentFileAsync(Guid.NewGuid(), Guid.NewGuid(), source, "invoice.txt", "text/plain");

        stored.StorageKey.Should().StartWith("private:"); stored.Sha256.Should().HaveLength(64);
        await using (Stream read = await store.OpenReadAsync(stored.StorageKey))
        using (var text = new StreamReader(read)) (await text.ReadToEndAsync()).Should().Be("private invoice");
        Action url = () => store.GetReadUrl(stored.StorageKey); url.Should().Throw<NotSupportedException>();
        await store.DeleteAsync(stored.StorageKey);
        Func<Task> missing = async () => { await using Stream _ = await store.OpenReadAsync(stored.StorageKey); };
        await missing.Should().ThrowAsync<InvalidOperationException>();
    }

    [Theory]
    [InlineData("documents", null)]
    [InlineData("documents-private", "https://public.example.test")]
    public async Task FinancialSource_Should_RefusePublicOrOverlappingStorage(string path, string? publicUrl)
    {
        var settings = new BlobStorageOptions { LocalBasePath = Path.Combine(_root, "App_Data") };
        settings.PrivateDocuments.Path = path; settings.PrivateDocuments.PublicBaseUrl = publicUrl;
        using var source = new MemoryStream([1]);

        Func<Task> upload = async () => { await Store(settings).UploadPrivateDocumentFileAsync(Guid.NewGuid(), Guid.NewGuid(), source, "invoice.pdf", "application/pdf"); };

        await upload.Should().ThrowAsync<InvalidOperationException>().WithMessage("Private file storage is unavailable.");
    }

    private DocumentFileStore Store(BlobStorageOptions settings)
    {
        var environment = new Mock<IWebHostEnvironment>(); environment.SetupGet(value => value.ContentRootPath).Returns(_root);
        environment.SetupGet(value => value.WebRootPath).Returns(Path.Combine(_root, "wwwroot"));
        var options = Options.Create(settings);
        return new(new BlobStorageFactoryService(options), options, environment.Object);
    }
    public void Dispose()
    {
        string target = Path.GetFullPath(_root);
        string temporary = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (target.StartsWith(temporary, StringComparison.OrdinalIgnoreCase) && Directory.Exists(target)) Directory.Delete(target, recursive: true);
    }
}
