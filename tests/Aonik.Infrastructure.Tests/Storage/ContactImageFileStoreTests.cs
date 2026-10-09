using Aonik.Application.Options;
using Aonik.Infrastructure.Storage;
using Aonik.Platform.Contracts.Models.ContactEnquiries;

using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using Moq;

namespace Aonik.Infrastructure.Tests.Storage;

public sealed class ContactImageFileStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aonik-contact-storage-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Upload_Should_RoundTripThroughPrivateStore_WithoutPublicUrl()
    {
        var settings = Settings();
        var store = Store(settings);
        using var original = new MemoryStream([1, 2, 3]);
        var uploaded = await store.UploadAsync(Guid.NewGuid(), Guid.NewGuid(), original, "image.jpg", "image/jpeg");
        await using var read = await store.OpenReadAsync(uploaded.StorageKey);
        using var bytes = new MemoryStream();
        await read!.CopyToAsync(bytes);
        bytes.ToArray().Should().Equal(1, 2, 3);
        uploaded.StorageKey.TrimStart('/').Should().StartWith("tenants/");
        Action publicUrl = () => store.GetUrl(uploaded.StorageKey);
        publicUrl.Should().Throw<NotSupportedException>();
        await store.DeleteAsync(uploaded.StorageKey);
        File.Exists(Path.Combine(settings.LocalBasePath, settings.ContactImages.Path, uploaded.StorageKey.TrimStart('/'))).Should().BeFalse();
    }

    [Theory]
    [InlineData("profiles")]
    [InlineData("profiles/nested")]
    [InlineData(".")]
    [InlineData("../wwwroot/private")]
    public async Task Upload_Should_RejectAnyOverlapWithPublicFolders(string path)
    {
        var settings = Settings();
        settings.ContactImages.Path = path;
        using var bytes = new MemoryStream([1]);
        var act = () => Store(settings).UploadAsync(Guid.NewGuid(), Guid.NewGuid(), bytes, "a.jpg", "image/jpeg");
        await act.Should().ThrowAsync<ContactEnquiryUnavailableException>();
    }

    [Fact]
    public async Task Upload_Should_RejectCustomWebRootAndConfiguredPublicUrl()
    {
        var settings = Settings();
        using var bytes = new MemoryStream([1]);
        var store = Store(settings, Path.Combine(settings.LocalBasePath, settings.ContactImages.Path));
        var act = () => store.UploadAsync(Guid.NewGuid(), Guid.NewGuid(), bytes, "a.jpg", "image/jpeg");
        await act.Should().ThrowAsync<ContactEnquiryUnavailableException>();
        settings.ContactImages.PublicBaseUrl = "https://public.example.com/photos";
        act = () => Store(settings).UploadAsync(Guid.NewGuid(), Guid.NewGuid(), bytes, "a.jpg", "image/jpeg");
        await act.Should().ThrowAsync<ContactEnquiryUnavailableException>();
    }

    [Theory]
    [InlineData("Azure", "products")]
    [InlineData("Azure", "")]
    [InlineData("unrecognised", "contact-private")]
    public async Task Upload_Should_FailClosedBeforeNetwork_ForUnsafeProviderConfiguration(string provider, string container)
    {
        var settings = Settings();
        settings.Provider = provider;
        settings.ContactImages.ContainerName = container;
        using var bytes = new MemoryStream([1]);
        var act = () => Store(settings).UploadAsync(Guid.NewGuid(), Guid.NewGuid(), bytes, "a.jpg", "image/jpeg");
        await act.Should().ThrowAsync<ContactEnquiryUnavailableException>();
    }

    private BlobStorageOptions Settings() => new() { LocalBasePath = Path.Combine(_root, "App_Data") };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Upload_Should_RejectPublicRootAliasToPrivateDirectory(bool webRoot)
    {
        // The Linux CI lane exercises symlinks without requiring Windows developer-mode privileges.
        if (OperatingSystem.IsWindows()) return;
        var settings = Settings();
        string privatePath = Path.Combine(settings.LocalBasePath, settings.ContactImages.Path);
        Directory.CreateDirectory(privatePath);
        string alias = Path.Combine(settings.LocalBasePath, webRoot ? "web-root" : settings.ProfilePhotos.Path);
        Directory.CreateSymbolicLink(alias, privatePath);
        try
        {
            using var bytes = new MemoryStream([1]);
            var act = () => Store(settings, webRoot ? alias : null)
                .UploadAsync(Guid.NewGuid(), Guid.NewGuid(), bytes, "a.jpg", "image/jpeg");
            await act.Should().ThrowAsync<ContactEnquiryUnavailableException>();
        }
        finally { Directory.Delete(alias); }
    }

    private ContactImageFileStore Store(BlobStorageOptions settings, string? webRoot = null)
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(x => x.ContentRootPath).Returns(_root);
        environment.SetupGet(x => x.WebRootPath).Returns(webRoot ?? string.Empty);
        var options = Options.Create(settings);
        return new ContactImageFileStore(new BlobStorageFactoryService(options), options, environment.Object);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
