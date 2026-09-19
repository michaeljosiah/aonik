using System.Security.Cryptography;
using System.Text;
using Aonik.Application.Abstractions.Storage;
using Aonik.Application.Options;
using Aonik.Infrastructure.Storage;
using FluentAssertions;
using FluentStorage.Blobs;
using Microsoft.Extensions.Options;
using Moq;

namespace Aonik.Infrastructure.Tests.Storage;

public class FileStoreStagingTests
{
    [Fact]
    public async Task Stage_Should_SupportProviderLengthAndRewind_WithoutHashingRetriesTwice()
    {
        var bytes = Encoding.UTF8.GetBytes("A garden among the stars");
        var storage = new Mock<IBlobStorage>();
        Stream? supplied = null;
        storage.Setup(s => s.WriteAsync(It.IsAny<string>(), It.IsAny<Stream>(), false, It.IsAny<CancellationToken>()))
            .Returns(async (string key, Stream stream, bool append, CancellationToken ct) =>
            {
                supplied = stream;
                stream.CanSeek.Should().BeTrue();
                stream.Length.Should().Be(bytes.Length);
                using var first = new MemoryStream();
                await stream.CopyToAsync(first, ct);
                stream.Position = 0;
                using var retry = new MemoryStream();
                await stream.CopyToAsync(retry, ct);
                retry.ToArray().Should().Equal(bytes);
            });
        var factory = new Mock<IBlobStorageFactory>();
        factory.Setup(f => f.Create(It.IsAny<ContentTypeOptions>())).Returns(storage.Object);
        var store = new FileStore(factory.Object, Options.Create(new BlobStorageOptions()), new ContentTypeOptions());
        using var input = new NonSeekable(bytes);
        var staged = await store.StageAsync(Guid.NewGuid(), input);
        staged.SizeBytes.Should().Be(bytes.Length);
        staged.ContentHash.Should().Be(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        supplied!.CanRead.Should().BeFalse();
    }

    private sealed class NonSeekable(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    }
}
