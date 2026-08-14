namespace Aonik.Workspaces.Services;

/// <summary>
/// Refuses to yield more than a declared number of bytes (Spec 091 §7).
///
/// <para>
/// The quota check runs against what the client <em>declared</em>. Without a bound, a caller declaring 1MB can
/// stream 4GB and the check has been outrun by the transfer — the bytes are on disk before anything notices, and
/// the ceiling refused nothing. Throwing mid-stream is the point: the object is never assembled, so nothing
/// partial can be promoted.
/// </para>
/// </summary>
internal sealed class BoundedReadStream : Stream
{
    private readonly Stream _inner;
    private readonly long _limit;

    public BoundedReadStream(Stream inner, long limit)
    {
        _inner = inner;
        _limit = limit;
    }

    public long BytesRead { get; private set; }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = _inner.Read(buffer, offset, count);
        Account(read);
        return read;
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var read = await _inner.ReadAsync(buffer, cancellationToken);
        Account(read);
        return read;
    }

    public override Task<int> ReadAsync(
        byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    private void Account(int read)
    {
        BytesRead += read;

        if (BytesRead > _limit)
        {
            throw new DeclaredLengthExceededException(_limit, BytesRead);
        }
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => BytesRead;
        set => throw new NotSupportedException();
    }

    public override void Flush() => _inner.Flush();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

/// <summary>
/// Reads a sequence of streams as one, opening each only when the previous is exhausted (Spec 091 §7).
///
/// <para>
/// This is what lets assembly stay streaming. Concatenating parts by reading them into a buffer would put a
/// whole take in memory at the one moment the file is largest — and a handful of concurrent multi-gigabyte
/// assemblies is not a slow request, it is an out-of-memory kill of the API process. That is a correctness issue
/// before it is a performance one.
/// </para>
/// </summary>
internal sealed class ConcatenatingStream : Stream
{
    private readonly IReadOnlyList<Func<CancellationToken, Task<Stream?>>> _openers;
    private int _index;
    private Stream? _current;

    public ConcatenatingStream(IReadOnlyList<Func<CancellationToken, Task<Stream?>>> openers)
        => _openers = openers;

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        while (true)
        {
            if (_current is null)
            {
                if (_index >= _openers.Count)
                {
                    return 0;
                }

                _current = await _openers[_index++](cancellationToken)
                    ?? throw new InvalidOperationException(
                        "A staged part is missing; the upload cannot be assembled.");
            }

            var read = await _current.ReadAsync(buffer, cancellationToken);

            if (read > 0)
            {
                return read;
            }

            await _current.DisposeAsync();
            _current = null;
        }
    }

    public override Task<int> ReadAsync(
        byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override int Read(byte[] buffer, int offset, int count)
        => ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _current?.Dispose();
        }

        base.Dispose(disposing);
    }
}

/// <summary>A caller streamed more than it declared (Spec 091 §7).</summary>
public sealed class DeclaredLengthExceededException : Exception
{
    public DeclaredLengthExceededException(long declared, long received)
        : base($"Upload declared {declared} bytes and streamed at least {received}; aborted at the bound.")
    {
        Declared = declared;
        Received = received;
    }

    public long Declared { get; }
    public long Received { get; }
}

/// <summary>
/// The assembled parts did not hash to what the client declared (Spec 091 §7, 089 §12).
///
/// <para>
/// Verification on promote, not on trust. Without it a client could upload its own bytes under someone else's
/// hash and then read that hash back as though it were theirs.
/// </para>
/// </summary>
public sealed class UploadHashMismatchException : Exception
{
    public UploadHashMismatchException(string declared, string actual)
        : base($"Upload declared hash {declared} but assembled to {actual}; the staged object was discarded.")
    {
        Declared = declared;
        Actual = actual;
    }

    public string Declared { get; }
    public string Actual { get; }
}
