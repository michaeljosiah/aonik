using System.Security.Cryptography;
using System.Text;

using Aonik.SharedKernel.Abstractions.Storage;
using Aonik.Workspaces.Services;

using FluentAssertions;

namespace Aonik.Application.Tests.Workspaces;

/// <summary>
/// Spec 089 §5 — the storage primitive the content-addressed key depends on.
///
/// <para>
/// <c>UploadAsync</c> could not serve this, and an earlier draft claiming otherwise hid a real prerequisite: it
/// returns a SHA-256 <em>after</em> writing to a randomly-named GUID path it chose itself, so the hash is an
/// output and never an input. Two identical uploads would write two physical objects and only the database row
/// would dedupe — leaving the second stranded and paid for.
/// </para>
/// </summary>
public class ContentAddressingTests
{
    /// <summary>
    /// A store that remembers objects by key, so promote semantics can be observed rather than assumed.
    /// </summary>
    private sealed class InMemoryFileStore : IFileStore
    {
        public Dictionary<string, byte[]> Objects { get; } = [];

        public Task<StagedBlob> StageAsync(
            Guid tenantId, Stream content, CancellationToken cancellationToken = default)
        {
            using var buffer = new MemoryStream();
            content.CopyTo(buffer);
            var bytes = buffer.ToArray();

            var key = $"workspaces/{tenantId:N}/staging/{Guid.NewGuid():N}";
            Objects[key] = bytes;

            var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            return Task.FromResult(new StagedBlob(tenantId, hash, bytes.Length, key));
        }

        public Task<PromoteResult> PromoteAsync(
            StagedBlob staged, string contentKey, CancellationToken cancellationToken = default)
        {
            if (Objects.ContainsKey(contentKey))
            {
                Objects.Remove(staged.TempKey);
                return Task.FromResult(
                    new PromoteResult(PromoteOutcome.AlreadyPresent, contentKey, staged.SizeBytes));
            }

            Objects[contentKey] = Objects[staged.TempKey];
            Objects.Remove(staged.TempKey);

            return Task.FromResult(new PromoteResult(PromoteOutcome.Stored, contentKey, staged.SizeBytes));
        }

        public Task<FileUploadResult> UploadAsync(
            Guid tenantId, Guid ownerEntityId, Stream fileStream, string fileName, string contentType,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
            => Task.FromResult<Stream?>(
                Objects.TryGetValue(storageKey, out var bytes) ? new MemoryStream(bytes) : null);

        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            Objects.Remove(storageKey);
            return Task.CompletedTask;
        }

        public string GetUrl(string storageKey) => storageKey;
    }

    private static Stream Content(string text) => new MemoryStream(Encoding.UTF8.GetBytes(text));

    [Fact]
    public async Task TheSameBytes_Should_PromoteToTheSameKey()
    {
        var store = new InMemoryFileStore();
        var tenantId = Guid.NewGuid();

        var first = await store.StageAsync(tenantId, Content("the undersong"));
        var second = await store.StageAsync(tenantId, Content("the undersong"));

        first.ContentHash.Should().Be(second.ContentHash);

        var key = WorkspaceBlobService.ContentKeyFor(tenantId, first.ContentHash);
        await store.PromoteAsync(first, key);
        var outcome = await store.PromoteAsync(second, key);

        // The whole concurrency answer: last writer discards rather than duplicates. Because the key IS
        // the hash, whoever got there first wrote byte-identical content and there is nothing to
        // reconcile.
        outcome.Outcome.Should().Be(PromoteOutcome.AlreadyPresent);

        store.Objects.Should().ContainSingle()
            .Which.Key.Should().Be(key, "identical bytes occupy one physical object, not two");
    }

    [Fact]
    public async Task DifferentBytes_Should_PromoteToDifferentKeys()
    {
        var store = new InMemoryFileStore();
        var tenantId = Guid.NewGuid();

        var a = await store.StageAsync(tenantId, Content("act one"));
        var b = await store.StageAsync(tenantId, Content("act two"));

        await store.PromoteAsync(a, WorkspaceBlobService.ContentKeyFor(tenantId, a.ContentHash));
        await store.PromoteAsync(b, WorkspaceBlobService.ContentKeyFor(tenantId, b.ContentHash));

        store.Objects.Should().HaveCount(2);
    }

    [Fact]
    public async Task Staging_Should_ReportTheHashOfWhatWasActuallyWritten()
    {
        var store = new InMemoryFileStore();
        var tenantId = Guid.NewGuid();
        const string text = "a reference image";

        var staged = await store.StageAsync(tenantId, Content(text));

        // Integrity by construction: a downloaded blob is verified against the hash that named it, so
        // a corrupted or substituted object is detected rather than trusted.
        staged.ContentHash.Should().Be(
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant());
        staged.SizeBytes.Should().Be(text.Length);
    }

    [Fact]
    public async Task Promotion_Should_LeaveNoStagedObjectBehind()
    {
        var store = new InMemoryFileStore();
        var tenantId = Guid.NewGuid();

        var staged = await store.StageAsync(tenantId, Content("one take"));
        await store.PromoteAsync(staged, WorkspaceBlobService.ContentKeyFor(tenantId, staged.ContentHash));

        // An abandoned staging object has no database row, so nothing else would ever find it. A
        // multi-gigabyte upload left behind is invisible and billed.
        store.Objects.Keys.Should().NotContain(staged.TempKey);
    }

    [Fact]
    public void TheContentKey_Should_ShardByTheFirstHashByte()
    {
        var tenantId = Guid.NewGuid();
        var hash = new string('a', 64);

        // Sharding is not decoration: a flat prefix with millions of objects is slow to list and, on
        // some stores, rate-limited per prefix.
        WorkspaceBlobService.ContentKeyFor(tenantId, hash)
            .Should().Be($"workspaces/{tenantId:N}/blobs/aa/{hash}");
    }
}
