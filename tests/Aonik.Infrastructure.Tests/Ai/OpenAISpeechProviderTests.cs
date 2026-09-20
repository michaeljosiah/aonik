using System.Net;
using System.Text.Json;
using Aonik.Infrastructure.Ai.Safety;
using Aonik.Infrastructure.Settings;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Safety;
using Aonik.SharedKernel.Abstractions.Settings;
using FluentAssertions;
using Moq;

namespace Aonik.Infrastructure.Tests.Ai;

public class OpenAISpeechProviderTests
{
    private static readonly string Audio = "data:audio/wav;base64," + Convert.ToBase64String(Wav());
    private const string Scores = """{"sexual":0,"graphic-violence":0,"frightening":0.8,"self-harm":0,"hate":0,"real-person-likeness":0,"csam":0}""";

    [Fact]
    public async Task Should_SendWholeAudioWithRoutedModel_WithoutStoringContent()
    {
        var handler = new Handler(Scores);
        var result = await Provider(handler).ScoreAsync("speech", Audio, "under-6", "routed-audio-model");
        result[SafetyCategories.Frightening].Should().Be(0.8);
        using var body = JsonDocument.Parse(handler.Body!);
        body.RootElement.GetProperty("model").GetString().Should().Be("routed-audio-model");
        body.RootElement.GetProperty("store").GetBoolean().Should().BeFalse();
        body.RootElement.GetProperty("messages")[1].GetProperty("content")[0].GetProperty("input_audio").GetProperty("data").GetString().Should().Be(Convert.ToBase64String(Wav()));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("not JSON")]
    public async Task Should_RefuseIncompleteOrMalformedScores(string answer)
    {
        await FluentActions.Awaiting(() => Provider(new Handler(answer)).ScoreAsync("speech", Audio, "under-6", "routed-model"))
            .Should().ThrowAsync<Exception>();
    }

    [Theory]
    [InlineData("https://example.com/audio.wav")]
    [InlineData("file:///private/audio.wav")]
    [InlineData("data:audio/wav;base64,")]
    [InlineData("data:audio/ogg;base64,AQID")]
    [InlineData("data:audio/wav;base64,invalid")]
    public async Task Should_RefuseUnsupportedReferencesBeforeNetwork(string audio)
    {
        var handler = new Handler(Scores);
        await FluentActions.Awaiting(() => Provider(handler).TranscribeAsync(audio,"routed-model")).Should().ThrowAsync<Exception>();
        handler.Body.Should().BeNull();
    }

    [Fact]
    public async Task Should_RefuseTruncationAndMissingCredentials()
    {
        await FluentActions.Awaiting(() => Provider(new Handler("partial", "length")).TranscribeAsync(Audio,"routed-model"))
            .Should().ThrowAsync<InvalidOperationException>();
        var handler = new Handler("words");
        await FluentActions.Awaiting(() => Provider(handler, null).TranscribeAsync(Audio,"routed-model"))
            .Should().ThrowAsync<InvalidOperationException>();
        handler.Body.Should().BeNull();
    }

    private static OpenAISpeechProvider Provider(Handler handler, string? key = "test-key")
    {
        var tenantSettings = new Mock<ITenantSettingStore>();
        var settings = new Mock<ISettingProvider>();
        settings.Setup(s=>s.GetAsync(AiSettingNames.OpenAiApiKey,It.IsAny<CancellationToken>())).ReturnsAsync(key);
        return new(new HttpClient(handler), new TenantFirstSettingReader(tenantSettings.Object,settings.Object,new Mock<ITenantProvider>().Object));
    }

    [Fact]
    public async Task Should_RefuseSilentAndTruncatedWavBeforeNetwork()
    {
        foreach (byte[] bytes in new[] {Wav(silent: true), Wav()[..^1]})
        {
            var handler = new Handler("invented words");
            await FluentActions.Awaiting(() => Provider(handler).TranscribeAsync("data:audio/wav;base64," + Convert.ToBase64String(bytes), "routed-model"))
                .Should().ThrowAsync<ArgumentException>();
            handler.Body.Should().BeNull();
        }
    }

    private static byte[] Wav(bool silent = false)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8); writer.Write(38); writer.Write("WAVEfmt "u8); writer.Write(16);
        writer.Write((short)1); writer.Write((short)1); writer.Write(24000); writer.Write(48000);
        writer.Write((short)2); writer.Write((short)16); writer.Write("data"u8); writer.Write(2); writer.Write((short)(silent ? 0 : 1));
        return stream.ToArray();
    }

    private sealed class Handler(string answer, string finish = "stop") : HttpMessageHandler
    {
        public string? Body { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new(HttpStatusCode.OK) {Content = new StringContent(JsonSerializer.Serialize(new {
                choices = new[] {new {finish_reason=finish,message=new {content=answer}}}
            }))};
        }
    }
}
