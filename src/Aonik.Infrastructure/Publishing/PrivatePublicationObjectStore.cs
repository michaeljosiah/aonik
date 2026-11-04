using System.Security.Cryptography;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;

using Aonik.Publishing.Contracts;
using Aonik.Publishing.Services;

namespace Aonik.Infrastructure.Publishing;

/// <summary>Private session-owned objects, never mounted by static-file middleware or exposed as URLs.</summary>
public sealed class PrivatePublicationObjectStore(IOptions<PublishingOptions> options, IHostEnvironment environment, IConfiguration configuration)
    : IPublishingObjectStore
{
    private static readonly SemaphoreSlim WriteSlots = new(4);
    public string LocationId
    {
        get
        {
            var root = RootPath();
            var marker = Path.Combine(root, ".store-id");
            RejectLinks(marker);
            if (!File.Exists(marker))
            {
                var temporary = marker + "." + Guid.NewGuid().ToString("N");
                try
                {
                    using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        file.Write(System.Text.Encoding.ASCII.GetBytes(Guid.NewGuid().ToString("N"))); file.Flush(true);
                    }
                    try { File.Move(temporary, marker, false); } catch (IOException) when (File.Exists(marker)) { }
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
            var identity = File.ReadAllText(marker);
            if (!Regex.IsMatch(identity, "^[a-f0-9]{32}$")) throw Unavailable();
            var canonical = OperatingSystem.IsWindows() ? root.ToUpperInvariant() : root;
            return Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonical + "\n" + identity)));
        }
    }

    public Task StorePartAsync(Guid tenantId, Guid sessionId, int number, string sha256, long bytes, Stream content, CancellationToken cancellationToken = default)
        => WriteAsync(PartPath(tenantId, sessionId, number, sha256), sha256, bytes, content, cancellationToken);

    public async Task<VerifiedInput> AssembleAsync(Guid tenantId, Guid sessionId, string sha256, long bytes,
        IReadOnlyList<(int Number, string Sha256)> parts, CancellationToken cancellationToken = default)
    {
        await using var sequence = new PartSequence(parts.Select(p => PartPath(tenantId, sessionId, p.Number, p.Sha256)).ToArray());
        var destination = PackagePath(tenantId, sessionId, sha256);
        await WriteAsync(destination, sha256, bytes, sequence, cancellationToken);
        return new VerifiedInput(destination, sha256, bytes);
    }

    public async Task<VerifiedInput> OpenPackageAsync(Guid tenantId, Guid sessionId, string sha256, long bytes, CancellationToken cancellationToken = default)
    {
        var path = PackagePath(tenantId, sessionId, sha256);
        await VerifyAsync(path, sha256, bytes, cancellationToken);
        return new VerifiedInput(path, sha256, bytes);
    }

    public Task DeleteSessionAsync(Guid tenantId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = SessionPath(tenantId, sessionId);
        RejectLinks(path);
        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        return Task.CompletedTask;
    }

    private string SessionPath(Guid tenantId, Guid sessionId)
    {
        if (tenantId == Guid.Empty || sessionId == Guid.Empty) throw new ArgumentException("A trusted storage scope is required.");
        var root = RootPath();
        var path = Path.GetFullPath(Path.Combine(root, tenantId.ToString("N"), sessionId.ToString("N")));
        if (!Inside(path, root)) throw Unavailable();
        RejectLinks(path);
        return path;
    }
    private string RootPath()
    {
        var configured = options.Value.Intake.PrivateRoot;
        if (string.IsNullOrWhiteSpace(configured) || !Path.IsPathFullyQualified(configured)) throw Unavailable();
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(configured));
        var publicPaths = new List<string> { Path.Combine(environment.ContentRootPath, "wwwroot"), Path.Combine(environment.ContentRootPath, "storage") };
        var blobRoot = configuration["BlobStorage:LocalBasePath"] ?? "storage";
        publicPaths.Add(Path.GetFullPath(blobRoot));
        publicPaths.Add(Path.GetFullPath(blobRoot, environment.ContentRootPath));
        foreach (var publicRoot in publicPaths)
        {
            var forbidden = Path.GetFullPath(publicRoot);
            if (Inside(root, forbidden) || Inside(forbidden, root)) throw Unavailable();
        }
        RejectLinks(root);
        Directory.CreateDirectory(root);
        if (!OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            File.SetUnixFileMode(root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        return root;
    }

    private string PartPath(Guid tenant, Guid session, int number, string hash)
    {
        ValidateHash(hash);
        if (number < 1) throw new ArgumentOutOfRangeException(nameof(number));
        return Path.Combine(SessionPath(tenant, session), $"part-{number}-{hash}");
    }
    private string PackagePath(Guid tenant, Guid session, string hash)
    {
        ValidateHash(hash);
        return Path.Combine(SessionPath(tenant, session), $"package-{hash}.zip");
    }
    private static void ValidateHash(string hash)
    {
        if (!Regex.IsMatch(hash, "^[a-f0-9]{64}$", RegexOptions.CultureInvariant)) throw new ArgumentException("Invalid content identity.");
    }
    private static bool Inside(string path, string root)
        => path.Equals(root, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    private static void RejectLinks(string path)
    {
        for (var current = new DirectoryInfo(path); current is not null; current = current.Parent)
            if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0) throw Unavailable();
        if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw Unavailable();
    }

    private static async Task WriteAsync(string destination, string sha256, long expectedBytes, Stream input, CancellationToken ct)
    {
        if (expectedBytes <= 0) throw Mismatch();
        await WriteSlots.WaitAsync(ct);
        var temporary = destination + ".upload-" + Guid.NewGuid().ToString("N");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            RejectLinks(destination);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long received = 0;
            await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous))
            {
                var buffer = new byte[65536];
                int read;
                while ((read = await input.ReadAsync(buffer, ct)) != 0)
                {
                    received = checked(received + read);
                    if (received > expectedBytes) throw Mismatch();
                    hash.AppendData(buffer, 0, read);
                    await file.WriteAsync(buffer.AsMemory(0, read), ct);
                }
                if (received != expectedBytes || Convert.ToHexStringLower(hash.GetHashAndReset()) != sha256) throw Mismatch();
                await file.FlushAsync(ct);
                file.Flush(flushToDisk: true);
            }
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(temporary,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
            try { File.Move(temporary, destination, overwrite: false); }
            catch (IOException) when (File.Exists(destination)) { await VerifyAsync(destination, sha256, expectedBytes, ct); }
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            finally { WriteSlots.Release(); }
        }
    }

    private static async Task VerifyAsync(string path, string hash, long length, CancellationToken ct)
    {
        RejectLinks(path);
        if (!File.Exists(path) || new FileInfo(path).Length != length) throw Mismatch();
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
        if (Convert.ToHexStringLower(await SHA256.HashDataAsync(file, ct)) != hash) throw Mismatch();
    }
    private static PublishingException Mismatch() => new(422, "upload-bytes-mismatch", "The uploaded length or digest does not match the declaration.");
    private static PublishingException Unavailable() => new(503, "private-storage-unavailable", "Private publication storage must be configured outside public file mounts.");

    private sealed class PartSequence(string[] paths) : Stream
    {
        private int _next;
        private FileStream? _current;
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            while (true)
            {
                if (_current is null)
                {
                    if (_next == paths.Length) return 0;
                    RejectLinks(paths[_next]);
                    _current = new FileStream(paths[_next++], FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
                }
                var read = await _current.ReadAsync(buffer, cancellationToken);
                if (read != 0) return read;
                await _current.DisposeAsync(); _current = null;
            }
        }
        public override ValueTask DisposeAsync() => _current?.DisposeAsync() ?? ValueTask.CompletedTask;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
