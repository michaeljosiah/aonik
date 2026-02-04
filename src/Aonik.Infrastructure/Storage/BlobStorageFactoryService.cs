using FluentStorage.Blobs;
using Microsoft.Extensions.Options;

using Aonik.Application.Abstractions.Storage;
using Aonik.Application.Options;

namespace Aonik.Infrastructure.Storage;

public class BlobStorageFactoryService : IBlobStorageFactory
{
    private readonly BlobStorageOptions _options;

    public BlobStorageFactoryService(IOptions<BlobStorageOptions> options)
    {
        _options = options.Value;
    }

    public FluentStorage.Blobs.IBlobStorage Create(ContentTypeOptions contentTypeOptions)
    {
        return BlobStorageFactory.Create(_options, contentTypeOptions);
    }
}
