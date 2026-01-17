using FluentStorage;
using FluentStorage.Blobs;

namespace Aonik.Infrastructure.Storage;

public static class CustomerProfileBlobStorageFactory
{
    public static IBlobStorage Create(string storagePath)
    {
        return StorageFactory.Blobs.DirectoryFiles(storagePath);
    }
}
