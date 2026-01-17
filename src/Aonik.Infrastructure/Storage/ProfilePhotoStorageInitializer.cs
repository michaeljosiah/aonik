using System.IO;

using FluentStorage;
using FluentStorage.Blobs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Aonik.Application.Services.Identity;

namespace Aonik.Infrastructure.Storage;

public class ProfilePhotoStorageInitializer : IHostedService
{
    private readonly IBlobStorage _blobStorage;
    private readonly CustomerProfileStorageOptions _options;

    public ProfilePhotoStorageInitializer(
        IBlobStorage blobStorage,
        IOptions<CustomerProfileStorageOptions> options)
    {
        _blobStorage = blobStorage;
        _options = options.Value;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var initPath = StoragePath.Combine(_options.BlobRootPath, ".init");
        await _blobStorage.WriteAsync(initPath, new MemoryStream(new byte[] { 0 }), false, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
