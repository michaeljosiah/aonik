using FluentStorage;
using FluentStorage.Blobs;

namespace Aonik.Infrastructure.Storage;

/// <summary>
/// Wraps an IBlobStorage instance and automatically prepends a path prefix
/// (typically an Azure Blob container name) to all blob paths.
/// This allows callers to use relative paths while the underlying storage
/// routes writes to the correct container.
/// </summary>
internal sealed class PrefixedBlobStorage : IBlobStorage
{
    private readonly IBlobStorage _inner;
    private readonly string _prefix;

    public PrefixedBlobStorage(IBlobStorage inner, string prefix)
    {
        _inner = inner;
        _prefix = prefix.TrimEnd('/');
    }

    private string Prefixed(string path) => StoragePath.Combine(_prefix, path);

    private IEnumerable<string> Prefixed(IEnumerable<string> paths) =>
        paths.Select(Prefixed);

    public async Task<IReadOnlyCollection<Blob>> ListAsync(
        ListOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ListOptions();
        options.FolderPath = Prefixed(options.FolderPath ?? string.Empty);
        return await _inner.ListAsync(options, cancellationToken);
    }

    public Task WriteAsync(string fullPath, Stream dataStream, bool append = false,
        CancellationToken cancellationToken = default) =>
        _inner.WriteAsync(Prefixed(fullPath), dataStream, append, cancellationToken);

    public Task<Stream> OpenReadAsync(string fullPath, CancellationToken cancellationToken = default) =>
        _inner.OpenReadAsync(Prefixed(fullPath), cancellationToken);

    public Task DeleteAsync(IEnumerable<string> fullPaths, CancellationToken cancellationToken = default) =>
        _inner.DeleteAsync(Prefixed(fullPaths), cancellationToken);

    public Task<IReadOnlyCollection<bool>> ExistsAsync(IEnumerable<string> fullPaths,
        CancellationToken cancellationToken = default) =>
        _inner.ExistsAsync(Prefixed(fullPaths), cancellationToken);

    public Task<IReadOnlyCollection<Blob>> GetBlobsAsync(IEnumerable<string> fullPaths,
        CancellationToken cancellationToken = default) =>
        _inner.GetBlobsAsync(Prefixed(fullPaths), cancellationToken);

    public Task SetBlobsAsync(IEnumerable<Blob> blobs, CancellationToken cancellationToken = default) =>
        _inner.SetBlobsAsync(
            blobs.Select(b => new Blob(Prefixed(b.FullPath))),
            cancellationToken);

    public Task<ITransaction> OpenTransactionAsync() =>
        _inner.OpenTransactionAsync();

    public void Dispose() => _inner.Dispose();
}
