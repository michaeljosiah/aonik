using System.IO;

using FluentStorage;
using FluentStorage.Blobs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Aonik.Application.Options;

namespace Aonik.Infrastructure.Storage;

public class ProfilePhotoStorageInitializer : IHostedService
{
    private readonly IBlobStorage _blobStorage;
    private readonly BlobStorageOptions _options;

    public ProfilePhotoStorageInitializer(
        IBlobStorage blobStorage,
        IOptions<BlobStorageOptions> options)
    {
        _blobStorage = blobStorage;
        _options = options.Value;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var initPath = StoragePath.Combine(_options.ProfilePhotos.Path, ".init");
        await _blobStorage.WriteAsync(initPath, new MemoryStream(new byte[] { 0 }), false, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
