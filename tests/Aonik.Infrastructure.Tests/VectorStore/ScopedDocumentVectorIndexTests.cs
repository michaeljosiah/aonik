namespace Aonik.Infrastructure.Tests.VectorStore;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aonik.Infrastructure.VectorStore;
using Aonik.Infrastructure.VectorStore.Contracts;
using Aonik.Infrastructure.VectorStore.Qdrant;
using Aonik.SharedKernel.Abstractions.Documents;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

/// <summary>
/// Covers the three correctness/isolation invariants of the party-scoped document index:
/// (1) chunk point ids must be valid, deterministic Qdrant ids; (2) Personal/Sensitive
/// retrieval must fail closed without an owner party (and purpose for Sensitive); and
/// (3) purge must remove every chunk, paging past the first scroll page.
/// </summary>
public sealed class ScopedDocumentVectorIndexTests
{
    private readonly Mock<IVectorStore> _vectorStore = new();
    private readonly Mock<IEmbeddingService> _embeddings = new();
    private readonly ScopedDocumentVectorIndex _sut;

    public ScopedDocumentVectorIndexTests()
    {
        _sut = new ScopedDocumentVectorIndex(
            _vectorStore.Object,
            _embeddings.Object,
            Options.Create(new QdrantConfiguration()),
            NullLogger<ScopedDocumentVectorIndex>.Instance);
    }

    // ── Fix #1: valid, deterministic UUID point ids ─────────────────────────────

    [Fact]
    public async Task IndexDocumentAsync_Should_Upsert_Chunks_With_Deterministic_Valid_Uuid_Point_Ids()
    {
        var documentId = Guid.NewGuid();
        var request = new DocumentIndexRequest(
            documentId, OwnerPartyId: Guid.NewGuid(), DocumentClassification.Internal,
            DocumentType: "bank_statement", Purpose: null, Chunks: new[] { "alpha", "beta" });

        var upsertedIds = new List<string>();
        StubEmptyScroll();
        StubBatchEmbeddings();
        _vectorStore
            .Setup(v => v.UpsertVectorAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<float[]>(),
                It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, float[], Dictionary<string, object>, CancellationToken>(
                (_, id, _, _, _) => upsertedIds.Add(id))
            .Returns(Task.CompletedTask);

        // Index the same document twice to assert determinism (re-index overwrites in place).
        await _sut.IndexDocumentAsync(request);
        await _sut.IndexDocumentAsync(request);

        upsertedIds.Should().HaveCount(4);
        foreach (var id in upsertedIds)
        {
            Guid.TryParse(id, out _).Should()
                .BeTrue($"'{id}' must be a valid UUID — Qdrant rejects ids like '<guid>:chunk:0'");
            id.Should().NotContain(":chunk:");
        }

        upsertedIds.Take(2).Should().OnlyHaveUniqueItems("each chunk gets its own point id");
        upsertedIds[0].Should().Be(upsertedIds[2], "chunk 0's id is stable across re-index");
        upsertedIds[1].Should().Be(upsertedIds[3], "chunk 1's id is stable across re-index");
    }

    [Fact]
    public async Task IndexDocumentAsync_Should_Carry_DocumentId_And_ChunkIndex_In_Payload()
    {
        var documentId = Guid.NewGuid();
        var request = new DocumentIndexRequest(
            documentId, Guid.NewGuid(), DocumentClassification.Internal, "bank_statement", null,
            new[] { "alpha", "beta" });

        var payloads = new List<Dictionary<string, object>>();
        StubEmptyScroll();
        StubBatchEmbeddings();
        _vectorStore
            .Setup(v => v.UpsertVectorAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<float[]>(),
                It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, float[], Dictionary<string, object>, CancellationToken>(
                (_, _, _, payload, _) => payloads.Add(payload))
            .Returns(Task.CompletedTask);

        await _sut.IndexDocumentAsync(request);

        payloads.Should().HaveCount(2);
        payloads[0]["document_id"].Should().Be(documentId.ToString());
        payloads[0]["chunk_index"].Should().Be(0);
        payloads[1]["chunk_index"].Should().Be(1);
    }

    [Fact]
    public async Task IndexDocumentAsync_Should_Purge_Stale_Vectors_Before_Re_Indexing_Fewer_Chunks()
    {
        // Prior extraction left three chunks; the new extraction has only one. The two trailing
        // chunks must be removed, not orphaned as still-searchable vectors.
        var request = new DocumentIndexRequest(
            Guid.NewGuid(), Guid.NewGuid(), DocumentClassification.Internal, "statement", null,
            new[] { "the-only-remaining-chunk" });

        var calls = new List<string>();
        _vectorStore
            .Setup(v => v.ScrollPageAsync(
                It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<int>(),
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VectorScrollPage(
                new List<VectorPointResult> { new("stale-0"), new("stale-1"), new("stale-2") }, null));
        _vectorStore
            .Setup(v => v.DeleteAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, Dictionary<string, object>, CancellationToken>(
                (_, id, _, _) => calls.Add($"delete:{id}"))
            .Returns(Task.CompletedTask);
        StubBatchEmbeddings();
        _vectorStore
            .Setup(v => v.UpsertVectorAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<float[]>(),
                It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, float[], Dictionary<string, object>, CancellationToken>(
                (_, _, _, _, _) => calls.Add("upsert"))
            .Returns(Task.CompletedTask);

        var indexed = await _sut.IndexDocumentAsync(request);

        indexed.Should().Be(1);
        calls.Should().ContainInOrder("delete:stale-0", "delete:stale-1", "delete:stale-2", "upsert");
        calls.Count(c => c == "upsert").Should().Be(1, "the single new chunk is written once, after the purge");
    }

    // ── Write-side scope guard: reject mis-scoped writes; skip non-embeddable classes ───────────

    [Theory]
    [InlineData(DocumentClassification.Personal)]
    [InlineData(DocumentClassification.Sensitive)]
    public async Task IndexDocumentAsync_Should_Reject_PartyScoped_Classification_With_Empty_Owner(
        DocumentClassification classification)
    {
        var request = new DocumentIndexRequest(
            Guid.NewGuid(), OwnerPartyId: Guid.Empty, classification, "tax_return",
            Purpose: "filing", Chunks: new[] { "chunk" });

        var act = async () => await _sut.IndexDocumentAsync(request);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*OwnerPartyId is required*");
        // Rejected before any side effect: nothing purged, nothing embedded or written.
        _vectorStore.Verify(
            v => v.ScrollPageAsync(
                It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<int>(),
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "an invalid request must not destructively purge the document's existing vectors");
        _vectorStore.Verify(
            v => v.UpsertVectorAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<float[]>(),
                It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task IndexDocumentAsync_Should_Reject_Sensitive_Without_Purpose()
    {
        var request = new DocumentIndexRequest(
            Guid.NewGuid(), OwnerPartyId: Guid.NewGuid(), DocumentClassification.Sensitive,
            "id_scan", Purpose: null, Chunks: new[] { "chunk" });

        var act = async () => await _sut.IndexDocumentAsync(request);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Purpose is required*");
    }

    [Theory]
    [InlineData(DocumentClassification.Sensitive)]
    [InlineData(DocumentClassification.Restricted)]
    public async Task IndexDocumentAsync_Should_Not_Embed_NonIndexable_Classifications(
        DocumentClassification classification)
    {
        // Sensitive is metadata-only until OCR + redaction; Restricted is never indexed. Neither
        // is embedded, but the document's existing vectors are still purged (replace semantics).
        var request = new DocumentIndexRequest(
            Guid.NewGuid(), OwnerPartyId: Guid.NewGuid(), classification, "id_scan",
            Purpose: "kyc", Chunks: new[] { "raw-unredacted-content" });
        StubEmptyScroll();

        var indexed = await _sut.IndexDocumentAsync(request);

        indexed.Should().Be(0);
        // The raw content never reaches the embedding service, and no vector is written.
        _embeddings.Verify(
            e => e.GetEmbeddingsBatchAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "non-indexable content must not be embedded");
        _vectorStore.Verify(
            v => v.UpsertVectorAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<float[]>(),
                It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        // The purge still ran, so any previously-indexed vectors for the document are removed.
        _vectorStore.Verify(
            v => v.ScrollPageAsync(
                It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<int>(),
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Fix #2: fail closed on under-scoped Personal/Sensitive retrieval ─────────

    [Fact]
    public async Task SearchAsync_Should_Reject_Personal_Scope_Without_OwnerParty()
    {
        var scope = new DocumentSearchScope(
            Guid.NewGuid(), Classifications: new[] { DocumentClassification.Personal });

        var act = async () => await _sut.SearchAsync("find my tax return", scope);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*OwnerPartyId is required*");
        _vectorStore.Verify(
            v => v.SearchAsync(
                It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<float>(),
                It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "retrieval must not run when the scope is rejected");
    }

    [Fact]
    public async Task SearchAsync_Should_Reject_Sensitive_Scope_Without_Purpose()
    {
        var scope = new DocumentSearchScope(
            Guid.NewGuid(), OwnerPartyId: Guid.NewGuid(),
            Classifications: new[] { DocumentClassification.Sensitive });

        var act = async () => await _sut.SearchAsync("find my id scan", scope);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Purpose is required*");
    }

    [Fact]
    public async Task SearchAsync_Should_Apply_OwnerParty_Filter_For_Valid_Personal_Scope()
    {
        var partyId = Guid.NewGuid();
        var scope = new DocumentSearchScope(
            Guid.NewGuid(), OwnerPartyId: partyId,
            Classifications: new[] { DocumentClassification.Personal });

        Dictionary<string, object>? capturedFilter = null;
        _embeddings
            .Setup(e => e.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { 0.1f });
        _vectorStore
            .Setup(v => v.SearchAsync(
                It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<float>(),
                It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .Callback<string, float[], int, float, Dictionary<string, object>, CancellationToken>(
                (_, _, _, _, filter, _) => capturedFilter = filter)
            .ReturnsAsync(new List<VectorSearchResult>());

        var results = await _sut.SearchAsync("find my tax return", scope);

        results.Should().BeEmpty();
        capturedFilter.Should().NotBeNull();
        JsonSerializer.Serialize(capturedFilter)
            .Should().Contain("owner_party_id").And.Contain(partyId.ToString());
    }

    [Fact]
    public async Task SearchAsync_Without_Owner_Or_Classifications_Should_Restrict_To_TenantWide_Classifications()
    {
        // No owner party and no classification filter: a tenant-wide search must NOT be able to
        // reach another party's Personal/Sensitive chunks that share the per-tenant collection.
        var scope = new DocumentSearchScope(Guid.NewGuid());

        Dictionary<string, object>? capturedFilter = null;
        _embeddings
            .Setup(e => e.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { 0.1f });
        _vectorStore
            .Setup(v => v.SearchAsync(
                It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<float>(),
                It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .Callback<string, float[], int, float, Dictionary<string, object>, CancellationToken>(
                (_, _, _, _, filter, _) => capturedFilter = filter)
            .ReturnsAsync(new List<VectorSearchResult>());

        await _sut.SearchAsync("anything in this tenant", scope);

        capturedFilter.Should().NotBeNull("a tenant-wide search must still constrain classification");
        var json = JsonSerializer.Serialize(capturedFilter);
        json.Should().Contain(nameof(DocumentClassification.Public));
        json.Should().Contain(nameof(DocumentClassification.Internal));
        json.Should().NotContain(nameof(DocumentClassification.Personal));
        json.Should().NotContain(nameof(DocumentClassification.Sensitive));
    }

    [Fact]
    public async Task SearchAsync_With_Owner_But_No_Classifications_Should_Exclude_Sensitive()
    {
        // The natural "general party RAG lookup": owner-scoped, no classification filter, no
        // purpose. It must include the party's non-sensitive content (Public/Internal/Personal)
        // but NOT Sensitive, which is only reachable via an explicit purpose-scoped request.
        var partyId = Guid.NewGuid();
        var scope = new DocumentSearchScope(Guid.NewGuid(), OwnerPartyId: partyId);

        Dictionary<string, object>? capturedFilter = null;
        _embeddings
            .Setup(e => e.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { 0.1f });
        _vectorStore
            .Setup(v => v.SearchAsync(
                It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<float>(),
                It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .Callback<string, float[], int, float, Dictionary<string, object>, CancellationToken>(
                (_, _, _, _, filter, _) => capturedFilter = filter)
            .ReturnsAsync(new List<VectorSearchResult>());

        await _sut.SearchAsync("what did I upload", scope);

        capturedFilter.Should().NotBeNull();
        var json = JsonSerializer.Serialize(capturedFilter);
        json.Should().Contain("owner_party_id").And.Contain(partyId.ToString());
        json.Should().Contain(nameof(DocumentClassification.Public));
        json.Should().Contain(nameof(DocumentClassification.Internal));
        json.Should().Contain(nameof(DocumentClassification.Personal));
        json.Should().NotContain(
            nameof(DocumentClassification.Sensitive),
            "Sensitive requires an explicit purpose scope and must not surface in a generic owner lookup");
    }

    [Fact]
    public async Task SearchAsync_Should_Allow_Sensitive_When_Owner_And_Purpose_Provided()
    {
        // The escape hatch: Sensitive IS reachable, but only when explicitly named with an owner
        // party and a purpose. Confirms the fail-closed default does not over-block.
        var scope = new DocumentSearchScope(
            Guid.NewGuid(), OwnerPartyId: Guid.NewGuid(),
            Purposes: new[] { "kyc-review" },
            Classifications: new[] { DocumentClassification.Sensitive });

        Dictionary<string, object>? capturedFilter = null;
        _embeddings
            .Setup(e => e.GetEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { 0.1f });
        _vectorStore
            .Setup(v => v.SearchAsync(
                It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<float>(),
                It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .Callback<string, float[], int, float, Dictionary<string, object>, CancellationToken>(
                (_, _, _, _, filter, _) => capturedFilter = filter)
            .ReturnsAsync(new List<VectorSearchResult>());

        var act = async () => await _sut.SearchAsync("passport scan", scope);

        await act.Should().NotThrowAsync();
        JsonSerializer.Serialize(capturedFilter)
            .Should().Contain(nameof(DocumentClassification.Sensitive)).And.Contain("kyc-review");
    }

    // ── Fix #3: purge pages past the first scroll page ──────────────────────────

    [Fact]
    public async Task PurgeDocumentAsync_Should_Delete_All_Vectors_Across_Multiple_Pages()
    {
        var page1 = new VectorScrollPage(
            new List<VectorPointResult> { new("id-1"), new("id-2") }, NextOffset: "cursor-1");
        var page2 = new VectorScrollPage(
            new List<VectorPointResult> { new("id-3") }, NextOffset: null);

        _vectorStore
            .SetupSequence(v => v.ScrollPageAsync(
                It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<int>(),
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(page1)
            .ReturnsAsync(page2);

        var deletedIds = new List<string>();
        _vectorStore
            .Setup(v => v.DeleteAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, Dictionary<string, object>, CancellationToken>(
                (_, id, _, _) => deletedIds.Add(id))
            .Returns(Task.CompletedTask);

        var purged = await _sut.PurgeDocumentAsync(Guid.NewGuid());

        purged.Should().Be(3, "every chunk across both pages is removed, not just the first page");
        deletedIds.Should().Equal("id-1", "id-2", "id-3");

        // The first scroll starts at a null offset; the second resumes from the returned cursor.
        _vectorStore.Verify(
            v => v.ScrollPageAsync(
                It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<int>(),
                null, It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _vectorStore.Verify(
            v => v.ScrollPageAsync(
                It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<int>(),
                "cursor-1", It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private void StubBatchEmbeddings()
        => _embeddings
            .Setup(e => e.GetEmbeddingsBatchAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> texts, CancellationToken _) =>
                texts.Select(_ => new[] { 0.1f }).ToList());

    // IndexDocumentAsync purges before writing, so index-path tests must stub the scroll the
    // purge issues; an empty page means "no existing vectors".
    private void StubEmptyScroll()
        => _vectorStore
            .Setup(v => v.ScrollPageAsync(
                It.IsAny<string>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<int>(),
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VectorScrollPage(new List<VectorPointResult>(), null));
}
