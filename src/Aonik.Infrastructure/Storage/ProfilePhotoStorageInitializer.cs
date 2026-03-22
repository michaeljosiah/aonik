using System.IO;

using FluentStorage;
using FluentStorage.Blobs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Aonik.Application.Abstractions.Storage;
using IBlobStorageFactory = Aonik.Application.Abstractions.Storage.IBlobStorageFactory;
using Aonik.Application.Options;

namespace Aonik.Infrastructure.Storage;

public class ProfilePhotoStorageInitializer : IHostedService
{
    private readonly IBlobStorage _blobStorage;
    private readonly BlobStorageOptions _options;
    private readonly ILogger<ProfilePhotoStorageInitializer> _logger;

    public ProfilePhotoStorageInitializer(
        IBlobStorageFactory blobStorageFactory,
        IOptions<BlobStorageOptions> options,
        ILogger<ProfilePhotoStorageInitializer> logger)
    {
        _options = options.Value;
        _blobStorage = blobStorageFactory.Create(_options.ProfilePhotos);
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var initPath = StoragePath.Combine(_options.ProfilePhotos.Path, ".init");
            await _blobStorage.WriteAsync(initPath, new MemoryStream(new byte[] { 0 }), false, cancellationToken);
            _logger.LogInformation("Profile photo storage initialized successfully");
        }
        catch (Exception ex)
        {
            // In containerized/production environments with local storage configured,
            // the filesystem may be read-only. Log and continue — the app can still
            // function; photo storage will fail at runtime if actually used.
            _logger.LogWarning(ex, "Failed to initialize profile photo storage. " +
                "If running in a container, consider configuring Azure Blob Storage instead of local storage");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
