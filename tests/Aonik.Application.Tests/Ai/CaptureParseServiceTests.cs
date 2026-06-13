using System.Runtime.CompilerServices;

using Aonik.Ai.Contracts.Models;
using Aonik.Ai.Services.Capture;
using Aonik.SharedKernel.Abstractions.Ai;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aonik.Application.Tests.Ai;

/// <summary>
/// Unit tests for <see cref="CaptureParseService"/> (Spec 047): structured-draft
/// extraction, the vision content path (a <see cref="DataContent"/> image part),
/// the unparseable fallback, and the AiRun audit bracket.
/// </summary>
public sealed class CaptureParseServiceTests
{
    private const string ParsedJson =
        """
        {"status":"parsed","draft":{"kind":"paymentLog","entityMatch":{"id":"ce_1","confidence":0.93},
        "commitmentMatch":{"id":"cm_9","confidence":0.88},"amount":{"value":200.00,"currency":"GBP"},
        "date":"2026-06-13","channel":"wise","note":"Wise transfer ref P2046-XK",
        "fieldConfidence":{"amount":0.98,"date":0.95,"entity":0.93}}}
        """;

    // 8-byte PNG signature + padding — enough for the magic-byte sniff.
    private static readonly string PngBase64 = Convert.ToBase64String(
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x00]);

    private static CaptureParseService CreateService(FakeChatClient chat, out FakeAiRunWriter runWriter)
    {
        runWriter = new FakeAiRunWriter();
        return new CaptureParseService(chat, runWriter, NullLogger<CaptureParseService>.Instance);
    }

    [Fact]
    public async Task ParseAsync_Should_ReturnStructuredDraft_When_ModelReturnsValidJson()
    {
        // Arrange
        var service = CreateService(new FakeChatClient(ParsedJson), out var runWriter);
        var request = new CaptureParseRequest(
            CaptureInputTypes.Text,
            "Sent £200 to Mum via Wise, ref P2046-XK",
            new CaptureHints(
                [new CaptureEntityHint("ce_1", "Mum")],
                [new CaptureCommitmentHint("cm_9", "Mum — monthly allowance", new CaptureMoney(200m, "GBP"), null)]));

        // Act
        var result = await service.ParseAsync(request);

        // Assert
        result.Status.Should().Be(CaptureParseStatuses.Parsed);
        result.Draft.Should().NotBeNull();
        result.Draft!.Kind.Should().Be("paymentLog");
        result.Draft.EntityMatch!.Id.Should().Be("ce_1");
        result.Draft.EntityMatch.Confidence.Should().BeApproximately(0.93, 0.001);
        result.Draft.Amount!.Value.Should().Be(200.00m);
        result.Draft.Amount.Currency.Should().Be("GBP");
        result.Draft.Channel.Should().Be("wise");
        result.Draft.FieldConfidence!["amount"].Should().BeApproximately(0.98, 0.001);
        // The audited AiRun id rides back on the proposal so the confirmed write can reference it.
        result.AiRunId.Should().Be(runWriter.LastRunId);
    }

    [Fact]
    public async Task ParseAsync_Should_AuditAsCaptureParse_RecordingInputShapeOnly()
    {
        // Arrange
        var service = CreateService(new FakeChatClient(ParsedJson), out var runWriter);
        var request = new CaptureParseRequest(CaptureInputTypes.Text, "Sent £200 to Mum", null);

        // Act
        await service.ParseAsync(request);

        // Assert — one run started as capture_parse; the input shape is recorded, never the payload.
        runWriter.Started.Should().ContainSingle();
        runWriter.Started[0].UseCase.Should().Be("capture_parse");
        runWriter.Started[0].InputRefsJson.Should().Contain("text");
        runWriter.Started[0].InputRefsJson.Should().NotContain("Mum");
        runWriter.Completed.Should().ContainSingle();
        runWriter.Failed.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsync_Should_ReturnUnparseable_When_ModelReturnsProse()
    {
        // Arrange — the stub-style placeholder is not JSON.
        var service = CreateService(new FakeChatClient("Sorry, I could not read that receipt."), out var runWriter);
        var request = new CaptureParseRequest(CaptureInputTypes.Text, "garbled", null);

        // Act
        var result = await service.ParseAsync(request);

        // Assert — capture never dead-ends; the run still completes (the model ran), not failed.
        result.Status.Should().Be(CaptureParseStatuses.Unparseable);
        result.Draft.Should().BeNull();
        runWriter.Completed.Should().ContainSingle();
        runWriter.Failed.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsync_Should_BuildMultimodalMessageWithDataContent_When_InputIsImage()
    {
        // Arrange
        var chat = new FakeChatClient(ParsedJson);
        var service = CreateService(chat, out _);
        var request = new CaptureParseRequest(CaptureInputTypes.Image, PngBase64, null);

        // Act
        await service.ParseAsync(request);

        // Assert — the user message carries an image DataContent part (the vision path).
        chat.Calls.Should().ContainSingle();
        var userMessage = chat.Calls[0].Single(m => m.Role == ChatRole.User);
        var imagePart = userMessage.Contents.OfType<DataContent>().SingleOrDefault();
        imagePart.Should().NotBeNull();
        imagePart!.MediaType.Should().Be("image/png");
        userMessage.Contents.OfType<TextContent>().Should().NotBeEmpty();
    }

    [Fact]
    public async Task ParseAsync_Should_ReturnUnparseable_WithoutCallingModel_When_ImageIsUndecodable()
    {
        // Arrange — not valid base64.
        var chat = new FakeChatClient(ParsedJson);
        var service = CreateService(chat, out var runWriter);
        var request = new CaptureParseRequest(CaptureInputTypes.Image, "!!! not base64 !!!", null);

        // Act
        var result = await service.ParseAsync(request);

        // Assert — undecodable image short-circuits to unparseable; the model is never called.
        result.Status.Should().Be(CaptureParseStatuses.Unparseable);
        result.Draft.Should().BeNull();
        chat.Calls.Should().BeEmpty();
        runWriter.Completed.Should().ContainSingle();
        runWriter.Failed.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsync_Should_MarkRunFailedAndRethrow_When_ModelThrows()
    {
        // Arrange
        var service = CreateService(new FakeChatClient(new InvalidOperationException("provider down")), out var runWriter);
        var request = new CaptureParseRequest(CaptureInputTypes.Text, "anything", null);

        // Act
        var act = async () => await service.ParseAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("provider down");
        runWriter.Failed.Should().ContainSingle();
        runWriter.Completed.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsync_Should_DowngradeToLowConfidence_When_ModelClaimsUnparseableButReturnsDraft()
    {
        // Arrange — contradictory output: unparseable status with a populated draft.
        const string contradictory =
            """{"status":"unparseable","draft":{"kind":"paymentLog","amount":{"value":50,"currency":"USD"},"fieldConfidence":{}}}""";
        var service = CreateService(new FakeChatClient(contradictory), out _);
        var request = new CaptureParseRequest(CaptureInputTypes.Text, "spent 50 dollars", null);

        // Act
        var result = await service.ParseAsync(request);

        // Assert — the draft is kept; the contradiction resolves to lowConfidence.
        result.Status.Should().Be(CaptureParseStatuses.LowConfidence);
        result.Draft.Should().NotBeNull();
        result.Draft!.Amount!.Value.Should().Be(50m);
    }

    // ── Fakes ────────────────────────────────────────────────────────

    private sealed class FakeChatClient : IChatClient
    {
        private readonly string _responseText;
        private readonly Exception? _throw;

        public FakeChatClient(string responseText) => _responseText = responseText;
        public FakeChatClient(Exception toThrow) { _throw = toThrow; _responseText = string.Empty; }

        public List<List<ChatMessage>> Calls { get; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Calls.Add(messages.ToList());
            if (_throw is not null)
            {
                throw _throw;
            }

            return Task.FromResult(new ChatResponse([new ChatMessage(ChatRole.Assistant, _responseText)]));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    private sealed class FakeAiRunWriter : IAiRunWriter
    {
        public List<(string UseCase, string InputRefsJson)> Started { get; } = [];
        public List<(Guid Id, int Tokens, int LatencyMs)> Completed { get; } = [];
        public List<(Guid Id, string Reason)> Failed { get; } = [];
        public Guid LastRunId { get; private set; }

        public Task<Guid> StartRunAsync(string useCase, string inputRefsJson, CancellationToken cancellationToken = default)
        {
            LastRunId = Guid.NewGuid();
            Started.Add((useCase, inputRefsJson));
            return Task.FromResult(LastRunId);
        }

        public Task MarkRunCompletedAsync(Guid aiRunId, string? outputRef = null, CancellationToken cancellationToken = default)
        {
            Completed.Add((aiRunId, 0, 0));
            return Task.CompletedTask;
        }

        public Task MarkRunCompletedWithMetricsAsync(
            Guid aiRunId, int tokensUsed, int latencyMs, decimal costEstimate,
            string? outputRef = null, CancellationToken cancellationToken = default)
        {
            Completed.Add((aiRunId, tokensUsed, latencyMs));
            return Task.CompletedTask;
        }

        public Task MarkRunFailedAsync(Guid aiRunId, string failureReason, CancellationToken cancellationToken = default)
        {
            Failed.Add((aiRunId, failureReason));
            return Task.CompletedTask;
        }

        public Task<Guid> SaveRunAsync(string useCase, string inputRefsJson, string outcome, CancellationToken cancellationToken = default)
            => StartRunAsync(useCase, inputRefsJson, cancellationToken);
    }
}
