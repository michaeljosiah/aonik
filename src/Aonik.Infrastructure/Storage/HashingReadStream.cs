using System.Security.Cryptography;

namespace Aonik.Infrastructure.Storage;

/// <summary>
/// A read-through decorator that hashes and counts bytes as they pass (Spec 089 §5).
///
/// <para>
/// It exists so staging can be <strong>single-pass</strong>. The obvious implementation reads the stream once to
/// hash it and again to write it, which needs a seekable source; the fallback for a non-seekable one is to buffer
/// the whole thing — and a world's takes are gigabytes. Hashing on the way past means the caller never chooses
/// between "seekable only" and "hold it all in memory".
/// </para>
/// </summary>
internal sealed class HashingReadStream : Stream
{
    private readonly Stream _inner;
    private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private string? _hex;

    public HashingReadStream(Stream inner) => _inner = inner;

    public long BytesRead { get; private set; }

    /// <summary>
    /// Lowercase hex SHA-256 of everything read so far. Finalises the hash, so read to the end first.
    /// </summary>
    public string GetHashHex()
        => _hex ??= Convert.ToHexString(_hash.GetHashAndReset()).ToLowerInvariant();

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = _inner.Read(buffer, offset, count);
        Observe(buffer.AsSpan(offset, read));
        return read;
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var read = await _inner.ReadAsync(buffer, cancellationToken);
        Observe(buffer.Span[..read]);
        return read;
    }

    public override Task<int> ReadAsync(
        byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    private void Observe(ReadOnlySpan<byte> chunk)
    {
        if (chunk.IsEmpty)
        {
            return;
        }

        _hash.AppendData(chunk);
        BytesRead += chunk.Length;
    }

    public override bool CanRead => true;

    // Deliberately not seekable, whatever the inner stream is: rewinding would replay bytes into the
    // hash and produce a digest for content nobody stored.
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hash.Dispose();
        }

        base.Dispose(disposing);
    }
}
