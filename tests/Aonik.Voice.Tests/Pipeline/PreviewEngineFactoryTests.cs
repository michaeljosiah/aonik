using Aonik.Voice.Pipeline;
using FluentAssertions;
using Voxa.Speech;
using Voxa.Speech.Azure;
using Voxa.Speech.ElevenLabs;
using Voxa.Speech.Mistral;
using Voxa.Speech.OpenAI;

namespace Aonik.Voice.Tests.Pipeline;

/// <summary>
/// Unit tests for the multi-provider preview engine factory used by the admin "Test STT/TTS"
/// surface. Verifies the per-vendor wiring without making any HTTP calls (engines are constructed
/// but never started).
/// </summary>
public class PreviewEngineFactoryTests
{
    private readonly IPreviewEngineFactory _factory = new PreviewEngineFactory();

    // ── TTS ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("openai", typeof(OpenAITextToSpeechEngine))]
    [InlineData("OpenAI", typeof(OpenAITextToSpeechEngine))]
    [InlineData("elevenlabs", typeof(ElevenLabsTextToSpeechEngine))]
    [InlineData("mistral", typeof(MistralTextToSpeechEngine))]
    public void CreateTtsEngine_Returns_Per_Vendor_Engine(string provider, Type expected)
    {
        var engine = _factory.CreateTtsEngine(new TtsPreviewEngineRequest(
            Provider: provider,
            ApiKey: "sk-test",
            VoiceId: "voice-id-here",
            ModelId: null,
            Region: null));

        engine.Should().BeOfType(expected);
    }

    [Fact]
    public void CreateTtsEngine_Azure_Returns_Azure_Engine_When_Region_Supplied()
    {
        var engine = _factory.CreateTtsEngine(new TtsPreviewEngineRequest(
            Provider: "azure",
            ApiKey: "key",
            VoiceId: null,
            ModelId: null,
            Region: "eastus"));

        engine.Should().BeOfType<AzureTextToSpeechEngine>();
    }

    [Fact]
    public void CreateTtsEngine_Azure_Without_Region_Throws()
    {
        Action act = () => _factory.CreateTtsEngine(new TtsPreviewEngineRequest(
            Provider: "azure",
            ApiKey: "key",
            VoiceId: null,
            ModelId: null,
            Region: null));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Region is required*azure*");
    }

    [Fact]
    public void CreateTtsEngine_ElevenLabs_Without_VoiceId_Throws()
    {
        Action act = () => _factory.CreateTtsEngine(new TtsPreviewEngineRequest(
            Provider: "elevenlabs",
            ApiKey: "key",
            VoiceId: null,
            ModelId: null,
            Region: null));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*ElevenLabs*voice id*");
    }

    [Fact]
    public void CreateTtsEngine_Unknown_Provider_Throws_NotSupported()
    {
        Action act = () => _factory.CreateTtsEngine(new TtsPreviewEngineRequest(
            Provider: "made-up",
            ApiKey: "key",
            VoiceId: null,
            ModelId: null,
            Region: null));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*made-up*not supported*");
    }

    // ── STT ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("openai")]
    [InlineData("openai-whisper")]
    [InlineData("OpenAI-Whisper")]
    public void CreateSttEngine_OpenAI_Returns_Whisper_Engine(string provider)
    {
        var engine = _factory.CreateSttEngine(new SttPreviewEngineRequest(
            Provider: provider,
            ApiKey: "sk-test",
            Model: "whisper-1",
            Language: "en",
            Region: null,
            InputSampleRate: 16000));

        engine.Should().BeOfType<OpenAIWhisperEngine>();
    }

    [Fact]
    public void CreateSttEngine_Azure_Returns_Azure_Engine_When_Region_Supplied()
    {
        var engine = _factory.CreateSttEngine(new SttPreviewEngineRequest(
            Provider: "azure",
            ApiKey: "key",
            Model: null,
            Language: "en-US",
            Region: "westeurope",
            InputSampleRate: 16000));

        engine.Should().BeOfType<AzureSpeechToTextEngine>();
    }

    [Fact]
    public void CreateSttEngine_Azure_Without_Region_Throws()
    {
        Action act = () => _factory.CreateSttEngine(new SttPreviewEngineRequest(
            Provider: "azure",
            ApiKey: "key",
            Model: null,
            Language: null,
            Region: null,
            InputSampleRate: 16000));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Region is required*azure*");
    }

    [Fact]
    public void CreateSttEngine_TTS_Provider_Name_Throws_NotSupported()
    {
        // 'elevenlabs' / 'mistral' are TTS-only, must not silently route to a fallback.
        Action act = () => _factory.CreateSttEngine(new SttPreviewEngineRequest(
            Provider: "elevenlabs",
            ApiKey: "key",
            Model: null,
            Language: null,
            Region: null,
            InputSampleRate: 16000));

        act.Should().Throw<NotSupportedException>();
    }

    // ── Output sample rate hint ────────────────────────────────────────────

    [Theory]
    [InlineData("openai", 24000)]
    [InlineData("azure", 24000)]
    [InlineData("elevenlabs", 24000)]
    [InlineData("mistral", 24000)]
    [InlineData("unknown-vendor", 24000)] // safe default
    public void GetTtsOutputSampleRate_Reports_Per_Vendor_Default(string provider, int expected)
    {
        _factory.GetTtsOutputSampleRate(provider).Should().Be(expected);
    }
}
