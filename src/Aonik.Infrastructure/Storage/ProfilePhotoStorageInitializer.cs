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
        Aonik.Application.Abstractions.Storage.IBlobStorageFactory blobStorageFactory,
        IOptions<BlobStorageOptions> options)
    {
        _options = options.Value;
        _blobStorage = blobStorageFactory.Create(_options.ProfilePhotos);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var initPath = StoragePath.Combine(_options.ProfilePhotos.Path, ".init");
        await _blobStorage.WriteAsync(initPath, new MemoryStream(new byte[] { 0 }), false, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
