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
        _logger.LogInformation(
            "Profile photo storage: Provider={Provider}, LocalBasePath={LocalBasePath}, " +
            "ContainerName={ContainerName}, PublicBaseUrl={PublicBaseUrl}",
            _options.Provider,
            _options.LocalBasePath,
            _options.ProfilePhotos.ContainerName,
            _options.ProfilePhotos.PublicBaseUrl ?? "(none — using local static files)");

        try
        {
            var initPath = StoragePath.Combine("customers", ".init");
            await _blobStorage.WriteAsync(initPath, new MemoryStream(new byte[] { 0 }), false, cancellationToken);
            _logger.LogInformation("Profile photo storage initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Profile photo storage initialization FAILED. Provider={Provider}. " +
                "Photo uploads will fail at runtime. " +
                "If running in a container, set BlobStorage__Provider=Azure and provide Azure credentials",
                _options.Provider);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
