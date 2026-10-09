using System.Collections.Concurrent;
using System.Security.Cryptography;

using Aonik.Platform.Contracts.Models.ContactEnquiries;
using Aonik.Platform.Contracts.Services.ContactEnquiries;
using Aonik.SharedKernel.Abstractions.Storage;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aonik.Api.Tests;

public sealed class ContactEnquiryTestFactory : CustomWebApplicationFactory
{
    public const int Permits = 8;
    public static readonly byte[] NormalizedImage = [255, 216, 255, 217];
    public SemaphoreSlim BlockedImages { get; } = new(0);
    public TaskCompletionSource? ReleaseImages { get; set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            new Dictionary<string, string?> { ["ContactEnquiries:RequestsPerMinute"] = Permits.ToString() }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IContactImageProcessor>();
            services.AddSingleton<IContactImageProcessor>(new TestImageProcessor(this));
            services.AddKeyedSingleton<IFileStore>(FileStoreKeys.ContactImages, new PrivateStore());
        });
    }

    private sealed class TestImageProcessor(ContactEnquiryTestFactory factory) : IContactImageProcessor
    {
        public async Task<ProcessedContactImage> ProcessAsync(ContactImageUpload image, CancellationToken cancellationToken = default)
        {
            if (image.FileName == "blocked.jpg")
            {
                factory.BlockedImages.Release();
                await factory.ReleaseImages!.Task.WaitAsync(cancellationToken);
            }
            if (image.Content.FirstOrDefault() == 13)
                throw new ContactImageValidationException("contact.image_invalid", "The image could not be accepted.");
            if (image.Content.FirstOrDefault() == 42)
                throw new ContactEnquiryUnavailableException();
            return new ProcessedContactImage(NormalizedImage, "image/jpeg", Path.GetFileNameWithoutExtension(image.FileName) + ".jpg");
        }
    }

    private sealed class PrivateStore : IFileStore
    {
        private readonly ConcurrentDictionary<string, byte[]> _files = new();
        public async Task<FileUploadResult> UploadAsync(Guid tenantId, Guid ownerEntityId, Stream fileStream,
            string fileName, string contentType, CancellationToken cancellationToken = default)
        {
            using var memory = new MemoryStream();
            await fileStream.CopyToAsync(memory, cancellationToken);
            var bytes = memory.ToArray();
            var key = $"{tenantId:N}/{ownerEntityId:N}/{Guid.NewGuid():N}.jpg";
            _files[key] = bytes;
            return new FileUploadResult("Test", "private-contact", key, contentType, fileName, bytes.Length,
                Convert.ToHexString(SHA256.HashData(bytes)));
        }
        public string GetUrl(string storageKey) => throw new InvalidOperationException("Contact images have no public URL.");
        public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
            => Task.FromResult<Stream?>(_files.TryGetValue(storageKey, out var bytes) ? new MemoryStream(bytes) : null);
        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            _files.TryRemove(storageKey, out _);
            return Task.CompletedTask;
        }
        public Task<StagedBlob> StageAsync(Guid tenantId, Stream content, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PromoteResult> PromoteAsync(StagedBlob staged, string contentKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
